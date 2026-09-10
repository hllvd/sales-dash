using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContractReconciliationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFileParserService _fileParserService;
        private readonly IContractStatusMapper _statusMapper;

        public ContractReconciliationController(
            AppDbContext context,
            IFileParserService fileParserService,
            IContractStatusMapper statusMapper)
        {
            _context = context;
            _fileParserService = fileParserService;
            _statusMapper = statusMapper;
        }

        [HttpPost("reconcile")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReconcileContracts(
            [FromForm] IFormFile file,
            [FromForm] DateTime startDate,
            [FromForm] DateTime endDate,
            [FromForm] Guid? userId,
            [FromForm] int? teamId,
            [FromForm] bool allowPartialNameMatch = false)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Nenhum arquivo enviado.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv")
            {
                return BadRequest("Formato de arquivo inválido. Por favor envie um arquivo .xlsx ou .csv.");
            }

            // Parse File Rows
            List<Dictionary<string, string>> rows;
            try
            {
                rows = await _fileParserService.ParseFileAsync(file);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao ler o arquivo: {ex.Message}");
            }

            if (rows == null || rows.Count == 0)
            {
                return BadRequest("O arquivo enviado está vazio.");
            }

            // Target User Lookup
            User? targetUser = null;
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                targetUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
            }

            // Target Team Lookup & Active Member IDs
            Team? targetTeam = null;
            HashSet<int>? activeTeamMemberInternalIds = null;
            if (teamId.HasValue && teamId.Value > 0)
            {
                targetTeam = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId.Value);
                if (targetTeam != null)
                {
                    var activeUserIds = await _context.UserTeams
                        .AsNoTracking()
                        .Where(ut => ut.TeamId == targetTeam.Id && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                        .Select(ut => ut.UserInternalId)
                        .ToListAsync();
                    activeTeamMemberInternalIds = activeUserIds.ToHashSet();
                }
            }

            // Preload system users and matriculas for matching
            var allUsers = await _context.Users.AsNoTracking().ToListAsync();
            var allMatriculas = await _context.UserMatriculas
                .AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.Matricula)
                .ToListAsync();

            var usersByEmail = allUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .ToLookup(u => u.Email.Trim().ToLowerInvariant());

            var usersByInternalId = allUsers
                .ToLookup(u => u.InternalId);

            var usersByNormalizedName = allUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Name))
                .ToLookup(u => NormalizeName(u.Name));

            var usersByMatricula = allMatriculas
                .Where(m => m.User != null && m.Matricula != null && !string.IsNullOrWhiteSpace(m.Matricula.MatriculaNumber))
                .ToLookup(m => m.Matricula.MatriculaNumber.Trim().ToLowerInvariant(), m => m.User!);

            // Query System Contracts for date range
            var startDateTime = startDate.Date;
            var endDateTime = endDate.Date.AddDays(1).AddTicks(-1);

            // Setup Column Headers Aliases
            var contractNumAliases = new[] { "contractnumber", "contrato", "numerocontrato", "numero do contrato", "número do contrato", "proposta", "codigo", "código", "number" };
            // Give top priority to "valor" (exact match first)
            var amountAliases = new[] { "valor", "valortotal", "valor total", "totalamount", "amount", "preco", "preço", "valor_total", "total" };
            var amountExcludedSubstrings = new[] { "parcela", "taxa", "entrada", "comissao", "comissão" };

            // Prioritize explicit seller/consultant columns before generic/matricula/email fields
            var sellerNameAliases = new[] { "consultor", "consultora", "vendedor", "vendedora", "comissionado", "comissionada", "assessor", "assessora", "corretor", "corretora" };
            var sellerSecondaryAliases = new[] { "useremail", "email", "e-mail", "matricula", "matrícula", "cpf", "userinternalid", "usuario", "usuário", "nome" };
            var sellerExcludedSubstrings = new[] { "nomepv", "nomedopv", "cliente", "nomedocliente", "pv" };

            var dateAliases = new[] { "date", "salestartdate", "datavenda", "data da venda", "data", "createdat" };
            var statusAliases = new[] { "status", "situacao", "situação", "estado", "rawstatus", "raw stats", "raw_status" };

            var contractNumbersInSheet = rows
                .Select(r => GetColumnValue(r, contractNumAliases)?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var allRelevantContracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.ContractStatus)
                .Where(c => (c.SaleStartDate >= startDateTime && c.SaleStartDate <= endDateTime)
                         || contractNumbersInSheet.Contains(c.ContractNumber))
                .ToListAsync();

            // Map user internal IDs to system user names
            var userIdToUserMap = allUsers.ToDictionary(u => u.InternalId);

            // Result sets
            var missingInSystem = new List<ReconciledContractItemDto>();
            var missingInImport = new List<ReconciledContractItemDto>();
            var amountMismatches = new List<AmountMismatchItemDto>();
            var dateMismatches = new DateMismatchItemDto[] { }.ToList();
            var sellerMismatches = new List<SellerMismatchItemDto>();
            var statusMismatches = new List<StatusMismatchItemDto>();
            var unassignedUserContracts = new List<ReconciledContractItemDto>();
            var userComparisonsMap = new Dictionary<string, (string displayName, decimal xlsxTotal, decimal sysTotal, int xlsxCount, int sysCount)>(StringComparer.OrdinalIgnoreCase);

            // System contract matching lookup across all relevant contracts in DB
            var systemContractsMap = new Dictionary<string, Contract>(StringComparer.OrdinalIgnoreCase);
            foreach (var sc in allRelevantContracts)
            {
                if (!string.IsNullOrWhiteSpace(sc.ContractNumber))
                {
                    systemContractsMap[sc.ContractNumber.Trim()] = sc;
                }
            }

            // Scoped system contracts for Missing In Import and system user comparisons
            var systemContracts = allRelevantContracts
                .Where(c => c.SaleStartDate >= startDateTime && c.SaleStartDate <= endDateTime)
                .Where(c => {
                    if (targetUser != null)
                        return c.UserInternalId == targetUser.InternalId;
                    if (activeTeamMemberInternalIds != null)
                        return c.UserInternalId.HasValue && activeTeamMemberInternalIds.Contains(c.UserInternalId.Value);
                    return true;
                })
                .ToList();

            using var fileStream = file.OpenReadStream();
            var detectedDateFormat = ReconciliationDateDetector.DetectDateFormat(
                rows,
                fileStream,
                dateAliases,
                contractNumAliases,
                systemContractsMap,
                GetColumnValue);

            var matchedSystemContractNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var contractNum = GetColumnValue(row, contractNumAliases);
                if (string.IsNullOrWhiteSpace(contractNum))
                    continue;

                contractNum = contractNum.Trim();
                var amountVal = ParseDecimal(GetColumnValue(row, amountAliases, amountExcludedSubstrings));
                var userVal = (GetColumnValue(row, sellerNameAliases, sellerExcludedSubstrings)
                               ?? GetColumnValue(row, sellerSecondaryAliases, sellerExcludedSubstrings))?.Trim();
                var dateVal = ReconciliationDateDetector.TryParseDateWithPattern(GetColumnValue(row, dateAliases), detectedDateFormat);

                // Resolve User for row
                User? rowUser = null;
                if (!string.IsNullOrWhiteSpace(userVal))
                {
                    var userKey = userVal.Trim().ToLowerInvariant();
                    var normalizedUserVal = NormalizeName(userVal);

                    rowUser = usersByEmail[userKey].FirstOrDefault()
                              ?? usersByMatricula[userKey].FirstOrDefault()
                              ?? usersByNormalizedName[normalizedUserVal].FirstOrDefault();

                    if (rowUser == null && int.TryParse(userVal, out int parsedInternalId))
                    {
                        rowUser = usersByInternalId[parsedInternalId].FirstOrDefault();
                    }

                    if (rowUser == null && allowPartialNameMatch && !string.IsNullOrWhiteSpace(normalizedUserVal))
                    {
                        var candidateMatches = allUsers
                            .Where(u => !string.IsNullOrWhiteSpace(u.Name) &&
                                        (NormalizeName(u.Name).Contains(normalizedUserVal) || normalizedUserVal.Contains(NormalizeName(u.Name))))
                            .ToList();

                        if (candidateMatches.Count == 1)
                        {
                            rowUser = candidateMatches[0];
                        }
                    }
                }

                // Check system contract existence
                systemContractsMap.TryGetValue(contractNum, out var systemContract);

                // Scope filtering: check if this row or its corresponding system contract belongs to the targeted scope
                if (targetUser != null)
                {
                    bool rowMatchesUser = (rowUser != null && rowUser.Id == targetUser.Id)
                        || (rowUser == null && !string.IsNullOrWhiteSpace(userVal) && NormalizeName(userVal) == NormalizeName(targetUser.Name))
                        || (rowUser == null && string.IsNullOrWhiteSpace(userVal));

                    bool systemMatchesUser = systemContract != null && systemContract.UserInternalId == targetUser.InternalId;

                    if (!rowMatchesUser && !systemMatchesUser)
                    {
                        continue;
                    }
                }
                else if (activeTeamMemberInternalIds != null)
                {
                    bool rowMatchesTeam = (rowUser != null && activeTeamMemberInternalIds.Contains(rowUser.InternalId))
                        || (rowUser == null && !string.IsNullOrWhiteSpace(userVal) && activeTeamMemberInternalIds.Any(id => userIdToUserMap.TryGetValue(id, out var tm) && NormalizeName(tm.Name) == NormalizeName(userVal)))
                        || (rowUser == null && string.IsNullOrWhiteSpace(userVal));

                    bool systemMatchesTeam = systemContract != null && systemContract.UserInternalId.HasValue && activeTeamMemberInternalIds.Contains(systemContract.UserInternalId.Value);

                    if (!rowMatchesTeam && !systemMatchesTeam)
                    {
                        continue;
                    }
                }

                var resolvedUserName = rowUser?.Name ?? targetUser?.Name ?? (string.IsNullOrWhiteSpace(userVal) ? null : userVal);
                var xlsxDisplayName = resolvedUserName ?? "Sem Usuário Atribuído";
                var xlsxUserKey = NormalizeName(xlsxDisplayName);
                userComparisonsMap.TryGetValue(xlsxUserKey, out var currentXlsx);
                userComparisonsMap[xlsxUserKey] = (
                    string.IsNullOrWhiteSpace(currentXlsx.displayName) ? xlsxDisplayName : currentXlsx.displayName,
                    currentXlsx.xlsxTotal + amountVal,
                    currentXlsx.sysTotal,
                    currentXlsx.xlsxCount + 1,
                    currentXlsx.sysCount
                );

                if (systemContract != null)
                {
                    matchedSystemContractNumbers.Add(contractNum);
                    var sysUser = systemContract.UserInternalId.HasValue && userIdToUserMap.TryGetValue(systemContract.UserInternalId.Value, out var u) ? u.Name : null;

                    // Unassigned user in system check:
                    // If contract exists in system but has no user assigned, and spreadsheet specified a seller/consultant
                    if (!systemContract.UserInternalId.HasValue || string.IsNullOrWhiteSpace(sysUser))
                    {
                        if (!string.IsNullOrWhiteSpace(userVal))
                        {
                            unassignedUserContracts.Add(new ReconciledContractItemDto
                            {
                                ContractNumber = contractNum,
                                TotalAmount = systemContract.TotalAmount > 0 ? systemContract.TotalAmount : amountVal,
                                UserIdentifier = userVal,
                                SystemUserName = null,
                                Date = dateVal ?? systemContract.SaleStartDate,
                                Source = "XLSX"
                            });
                        }
                    }
                    else
                    {
                        // System HAS an assigned user
                        // Check seller mismatch (XLSX seller differs from system user)
                        bool sellerMismatch = false;
                        if (rowUser != null)
                        {
                            sellerMismatch = (rowUser.InternalId != systemContract.UserInternalId.Value);
                        }
                        else if (!string.IsNullOrWhiteSpace(userVal))
                        {
                            sellerMismatch = !string.Equals(NormalizeName(userVal), NormalizeName(sysUser), StringComparison.OrdinalIgnoreCase);
                        }

                        if (sellerMismatch)
                        {
                            sellerMismatches.Add(new SellerMismatchItemDto
                            {
                                ContractNumber = contractNum,
                                TotalAmount = systemContract.TotalAmount,
                                SystemUserName = sysUser,
                                XlsxUserIdentifier = rowUser?.Name ?? userVal,
                                SaleStartDate = systemContract.SaleStartDate
                            });
                        }
                    }

                    // Check amount mismatch
                    if (Math.Abs(systemContract.TotalAmount - amountVal) > 0.01m)
                    {
                        amountMismatches.Add(new AmountMismatchItemDto
                        {
                            ContractNumber = contractNum,
                            SystemAmount = systemContract.TotalAmount,
                            XlsxAmount = amountVal,
                            UserIdentifier = userVal ?? resolvedUserName,
                            SystemUserName = sysUser ?? resolvedUserName,
                            SaleStartDate = systemContract.SaleStartDate
                        });
                    }

                    // Check date mismatch (ignoring time)
                    if (dateVal.HasValue && systemContract.SaleStartDate.Date != dateVal.Value.Date)
                    {
                        dateMismatches.Add(new DateMismatchItemDto
                        {
                            ContractNumber = contractNum,
                            TotalAmount = systemContract.TotalAmount,
                            SystemDate = systemContract.SaleStartDate,
                            XlsxDate = dateVal.Value,
                            SystemUserName = sysUser ?? resolvedUserName
                        });
                    }

                    // Check status mismatch (using Contract.ContractStatusId / ContractStatus.Name)
                    var statusVal = GetColumnValue(row, statusAliases)?.Trim();
                    var systemStatus = systemContract.ContractStatus?.Name?.Trim();

                    if (!string.IsNullOrWhiteSpace(statusVal) || !string.IsNullOrWhiteSpace(systemStatus))
                    {
                        var xlsxCanonical = !string.IsNullOrWhiteSpace(statusVal) ? _statusMapper.MapStatus(statusVal) : null;
                        bool matches = string.Equals(systemStatus ?? string.Empty, statusVal ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                    || (!string.IsNullOrWhiteSpace(xlsxCanonical) && string.Equals(systemStatus ?? string.Empty, xlsxCanonical, StringComparison.OrdinalIgnoreCase));

                        if (!matches)
                        {
                            statusMismatches.Add(new StatusMismatchItemDto
                            {
                                ContractNumber = contractNum,
                                TotalAmount = systemContract.TotalAmount,
                                SystemStatus = systemStatus,
                                XlsxStatus = statusVal,
                                SystemUserName = sysUser ?? resolvedUserName,
                                SaleStartDate = systemContract.SaleStartDate
                            });
                        }
                    }
                }
                else
                {
                    // In XLSX but NOT in System
                    missingInSystem.Add(new ReconciledContractItemDto
                    {
                        ContractNumber = contractNum,
                        TotalAmount = amountVal,
                        UserIdentifier = userVal ?? resolvedUserName,
                        SystemUserName = resolvedUserName,
                        Date = dateVal,
                        Source = "XLSX"
                    });
                }
            }

            // Contracts in System (all tracked for user comparison, plus missingInImport check)
            foreach (var sc in systemContracts)
            {
                var sysUser = sc.UserInternalId.HasValue && userIdToUserMap.TryGetValue(sc.UserInternalId.Value, out var u) ? u.Name : (targetUser?.Name ?? "Sem Usuário Atribuído");
                var sysUserKey = NormalizeName(sysUser);

                userComparisonsMap.TryGetValue(sysUserKey, out var currentSys);
                userComparisonsMap[sysUserKey] = (
                    string.IsNullOrWhiteSpace(currentSys.displayName) ? sysUser : currentSys.displayName,
                    currentSys.xlsxTotal,
                    currentSys.sysTotal + sc.TotalAmount,
                    currentSys.xlsxCount,
                    currentSys.sysCount + 1
                );

                if (!string.IsNullOrWhiteSpace(sc.ContractNumber) && !matchedSystemContractNumbers.Contains(sc.ContractNumber.Trim()))
                {
                    missingInImport.Add(new ReconciledContractItemDto
                    {
                        ContractNumber = sc.ContractNumber,
                        TotalAmount = sc.TotalAmount,
                        UserIdentifier = sysUser,
                        SystemUserName = sysUser,
                        Date = sc.SaleStartDate,
                        Source = "System"
                    });
                }
            }

            // Ensure all members of selected team or specific user are in user comparison (even with 0 contracts)
            if (activeTeamMemberInternalIds != null)
            {
                foreach (var memberId in activeTeamMemberInternalIds)
                {
                    if (userIdToUserMap.TryGetValue(memberId, out var memberUser) && !string.IsNullOrWhiteSpace(memberUser.Name))
                    {
                        var memberKey = NormalizeName(memberUser.Name);
                        if (!userComparisonsMap.ContainsKey(memberKey))
                        {
                            userComparisonsMap[memberKey] = (memberUser.Name, 0m, 0m, 0, 0);
                        }
                    }
                }
            }
            else if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Name))
            {
                var targetKey = NormalizeName(targetUser.Name);
                if (!userComparisonsMap.ContainsKey(targetKey))
                {
                    userComparisonsMap[targetKey] = (targetUser.Name, 0m, 0m, 0, 0);
                }
            }

            var result = new ContractReconciliationResultDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TargetUserId = targetUser?.Id,
                TargetUserName = targetUser?.Name,
                TargetTeamId = targetTeam?.Id,
                TargetTeamName = targetTeam?.Name,

                MissingInSystemSummary = new ReconciliationCategorySummaryDto
                {
                    Count = missingInSystem.Count,
                    TotalAmount = missingInSystem.Sum(x => x.TotalAmount)
                },
                MissingInImportSummary = new ReconciliationCategorySummaryDto
                {
                    Count = missingInImport.Count,
                    TotalAmount = missingInImport.Sum(x => x.TotalAmount)
                },
                AmountMismatchSummary = new ReconciliationCategorySummaryDto
                {
                    Count = amountMismatches.Count,
                    TotalAmount = amountMismatches.Sum(x => x.Difference)
                },
                DateMismatchSummary = new ReconciliationCategorySummaryDto
                {
                    Count = dateMismatches.Count,
                    TotalAmount = dateMismatches.Sum(x => x.TotalAmount)
                },
                SellerMismatchSummary = new ReconciliationCategorySummaryDto
                {
                    Count = sellerMismatches.Count,
                    TotalAmount = sellerMismatches.Sum(x => x.TotalAmount)
                },
                StatusMismatchSummary = new ReconciliationCategorySummaryDto
                {
                    Count = statusMismatches.Count,
                    TotalAmount = statusMismatches.Sum(x => x.TotalAmount)
                },
                UnassignedUserSummary = new ReconciliationCategorySummaryDto
                {
                    Count = unassignedUserContracts.Count,
                    TotalAmount = unassignedUserContracts.Sum(x => x.TotalAmount)
                },

                MissingInSystem = missingInSystem,
                MissingInImport = missingInImport,
                AmountMismatches = amountMismatches,
                DateMismatches = dateMismatches,
                SellerMismatches = sellerMismatches,
                StatusMismatches = statusMismatches,
                UnassignedUserContracts = unassignedUserContracts,

                UserComparisons = userComparisonsMap.Values
                    .Select(v => new UserComparisonItemDto
                    {
                        UserName = v.displayName,
                        XlsxTotal = v.xlsxTotal,
                        SystemTotal = v.sysTotal,
                        XlsxCount = v.xlsxCount,
                        SystemCount = v.sysCount
                    })
                    .OrderByDescending(x => x.XlsxTotal)
                    .ThenByDescending(x => x.SystemTotal)
                    .ToList()
            };

            return Ok(result);
        }

        [HttpPost("export-xlsx")]
        public IActionResult ExportTabXlsx([FromBody] ExportReconciliationTabRequestDto request)
        {
            if (request == null || request.Headers == null || request.Headers.Count == 0)
            {
                return BadRequest("Dados inválidos para exportação.");
            }

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new ExcelPackage();
            var rawTitle = string.IsNullOrWhiteSpace(request.Title) ? "Reconciliação" : request.Title;
            var cleanTitle = Regex.Replace(rawTitle, @"[\\/?*:[\]]", " ").Trim();
            if (cleanTitle.Length > 30) cleanTitle = cleanTitle.Substring(0, 30);
            if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = "Reconciliação";

            var worksheet = package.Workbook.Worksheets.Add(cleanTitle);

            // Header row
            for (int col = 0; col < request.Headers.Count; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = request.Headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }

            // Data rows
            if (request.Rows != null)
            {
                for (int r = 0; r < request.Rows.Count; r++)
                {
                    var row = request.Rows[r];
                    for (int c = 0; c < row.Count && c < request.Headers.Count; c++)
                    {
                        var cell = worksheet.Cells[r + 2, c + 1];
                        var header = request.Headers[c].ToLowerInvariant();
                        var valStr = row[c]?.Trim();

                        if (string.IsNullOrWhiteSpace(valStr))
                        {
                            cell.Value = string.Empty;
                            continue;
                        }

                        // Identifier / code columns should remain text to preserve leading zeros
                        bool isCodeOrId = header.Contains("número") || header.Contains("numero") ||
                                         header.Contains("contrato") || header.Contains("matrícula") ||
                                         header.Contains("matricula") || header.Contains("código") ||
                                         header.Contains("codigo") || header.Contains("cota") ||
                                         header.Contains("grupo");

                        if (!isCodeOrId && decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal numVal))
                        {
                            cell.Value = numVal;
                            if (numVal % 1 == 0 && !valStr.Contains("."))
                            {
                                cell.Style.Numberformat.Format = "#,##0";
                            }
                            else
                            {
                                cell.Style.Numberformat.Format = "#,##0.00";
                            }
                        }
                        else
                        {
                            cell.Value = valStr;
                        }
                    }
                }
            }

            worksheet.Cells.AutoFitColumns();
            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        [HttpPost("detect-date-range")]
        public async Task<IActionResult> DetectDateRange([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Nenhum arquivo enviado.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv")
            {
                return BadRequest("Formato de arquivo inválido. Por favor envie um arquivo .xlsx ou .csv.");
            }

            List<Dictionary<string, string>> rows;
            try
            {
                rows = await _fileParserService.ParseFileAsync(file);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao ler o arquivo: {ex.Message}");
            }

            if (rows == null || rows.Count == 0)
            {
                return Ok(new DetectDateRangeResponseDto
                {
                    StartDate = null,
                    EndDate = null,
                    DetectedFormat = null,
                    TotalRows = 0
                });
            }

            var contractNumAliases = new[] { "contractnumber", "contrato", "numerocontrato", "numero do contrato", "número do contrato", "proposta", "codigo", "código", "number" };
            var dateAliases = new[] { "date", "salestartdate", "datavenda", "data da venda", "data", "createdat" };

            var contractNumbersInSheet = rows
                .Select(r => GetColumnValue(r, contractNumAliases)?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var relevantContracts = await _context.Contracts
                .AsNoTracking()
                .Where(c => contractNumbersInSheet.Contains(c.ContractNumber))
                .ToListAsync();

            var systemContractsMap = new Dictionary<string, Contract>(StringComparer.OrdinalIgnoreCase);
            foreach (var sc in relevantContracts)
            {
                if (!string.IsNullOrWhiteSpace(sc.ContractNumber))
                {
                    systemContractsMap[sc.ContractNumber.Trim()] = sc;
                }
            }

            using var fileStream = file.OpenReadStream();
            var (minDate, maxDate, detectedFormat) = ReconciliationDateDetector.DetectDateRange(
                rows,
                fileStream,
                dateAliases,
                contractNumAliases,
                systemContractsMap,
                GetColumnValue);

            return Ok(new DetectDateRangeResponseDto
            {
                StartDate = minDate?.ToString("yyyy-MM-dd"),
                EndDate = maxDate?.ToString("yyyy-MM-dd"),
                DetectedFormat = detectedFormat,
                TotalRows = rows.Count
            });
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var normalizedString = name.Trim().Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var text = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static string? GetColumnValue(Dictionary<string, string> row, string[] aliases, string[]? excludedSubstrings = null)
        {
            var normalizedRow = new List<(string originalKey, string normalizedKey, string value)>();
            foreach (var kvp in row)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

                var normKey = kvp.Key.Trim().ToLowerInvariant()
                    .Replace("_", "")
                    .Replace("-", "")
                    .Replace(" ", "");
                normalizedRow.Add((kvp.Key, normKey, kvp.Value));
            }

            // 1st Pass: EXACT MATCH (aliases in prioritized order)
            foreach (var alias in aliases)
            {
                var normAlias = alias.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
                foreach (var item in normalizedRow)
                {
                    if (item.normalizedKey == normAlias)
                    {
                        if (excludedSubstrings != null && excludedSubstrings.Any(exc => item.normalizedKey.Contains(exc)))
                            continue;
                        return item.value;
                    }
                }
            }

            // 2nd Pass: SUBSTRING MATCH (aliases in prioritized order)
            foreach (var alias in aliases)
            {
                var normAlias = alias.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
                foreach (var item in normalizedRow)
                {
                    if (item.normalizedKey.Contains(normAlias))
                    {
                        if (excludedSubstrings != null && excludedSubstrings.Any(exc => item.normalizedKey.Contains(exc)))
                            continue;
                        return item.value;
                    }
                }
            }

            return null;
        }

        private static decimal ParseDecimal(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return 0m;

            var clean = rawValue.Trim().Replace("R$", "").Replace("$", "").Trim();

            // Detect separator formats when both ',' and '.' exist
            if (clean.Contains(",") && clean.Contains("."))
            {
                int lastDot = clean.LastIndexOf('.');
                int lastComma = clean.LastIndexOf(',');

                if (lastDot > lastComma)
                {
                    // e.g. "140,000.00" -> comma is thousand separator, dot is decimal separator
                    clean = clean.Replace(",", "");
                }
                else
                {
                    // e.g. "140.000,00" -> dot is thousand separator, comma is decimal separator
                    clean = clean.Replace(".", "").Replace(",", ".");
                }
            }
            else if (clean.Contains(","))
            {
                // e.g. "140000,00" or "140,50" -> comma is decimal separator
                clean = clean.Replace(",", ".");
            }

            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            return 0m;
        }

        private static DateTime? ParseDateTime(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (DateTime.TryParse(rawValue, new CultureInfo("pt-BR"), DateTimeStyles.None, out var dtPt))
                return dtPt;

            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            return null;
        }
    }
}
