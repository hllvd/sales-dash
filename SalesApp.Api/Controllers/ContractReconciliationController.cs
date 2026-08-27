using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public ContractReconciliationController(AppDbContext context, IFileParserService fileParserService)
        {
            _context = context;
            _fileParserService = fileParserService;
        }

        [HttpPost("reconcile")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReconcileContracts(
            [FromForm] IFormFile file,
            [FromForm] DateTime startDate,
            [FromForm] DateTime endDate,
            [FromForm] Guid? userId,
            [FromForm] int? teamId)
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

            var usersByName = allUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Name))
                .ToLookup(u => u.Name.Trim().ToLowerInvariant());

            var usersByMatricula = allMatriculas
                .Where(m => m.User != null && m.Matricula != null && !string.IsNullOrWhiteSpace(m.Matricula.MatriculaNumber))
                .ToLookup(m => m.Matricula.MatriculaNumber.Trim().ToLowerInvariant(), m => m.User!);

            // Query System Contracts for date range
            var startDateTime = startDate.Date;
            var endDateTime = endDate.Date.AddDays(1).AddTicks(-1);

            var contractsQuery = _context.Contracts
                .AsNoTracking()
                .Where(c => c.SaleStartDate >= startDateTime && c.SaleStartDate <= endDateTime);

            if (targetUser != null)
            {
                contractsQuery = contractsQuery.Where(c => c.UserInternalId == targetUser.InternalId);
            }
            else if (activeTeamMemberInternalIds != null)
            {
                contractsQuery = contractsQuery.Where(c => c.UserInternalId.HasValue && activeTeamMemberInternalIds.Contains(c.UserInternalId.Value));
            }

            var systemContracts = await contractsQuery.ToListAsync();

            // Map user internal IDs to system user names
            var userIdToUserMap = allUsers.ToDictionary(u => u.InternalId);

            // Setup Column Headers Aliases
            var contractNumAliases = new[] { "contractnumber", "contrato", "numerocontrato", "numero do contrato", "número do contrato", "proposta", "codigo", "código", "number" };
            var amountAliases = new[] { "totalamount", "valor", "valortotal", "valor total", "amount", "preco", "preço", "valor_total" };
            var userAliases = new[] { "useremail", "email", "e-mail", "matricula", "matrícula", "cpf", "userinternalid", "usuario", "usuário", "vendedor", "nome" };
            var dateAliases = new[] { "date", "salestartdate", "datavenda", "data da venda", "data", "createdat" };

            // Result sets
            var missingInSystem = new List<ReconciledContractItemDto>();
            var missingInImport = new List<ReconciledContractItemDto>();
            var amountMismatches = new List<AmountMismatchItemDto>();
            var dateMismatches = new List<DateMismatchItemDto>();
            var sellerMismatches = new List<SellerMismatchItemDto>();
            var unassignedUserContracts = new List<ReconciledContractItemDto>();

            // System contract matching lookup
            // Key: ContractNumber (Trim + Lower) -> Contract entity
            var systemContractsMap = new Dictionary<string, Contract>(StringComparer.OrdinalIgnoreCase);
            foreach (var sc in systemContracts)
            {
                if (!string.IsNullOrWhiteSpace(sc.ContractNumber))
                {
                    systemContractsMap[sc.ContractNumber.Trim()] = sc;
                }
            }

            var matchedSystemContractNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var contractNum = GetColumnValue(row, contractNumAliases);
                if (string.IsNullOrWhiteSpace(contractNum))
                    continue;

                contractNum = contractNum.Trim();
                var amountVal = ParseDecimal(GetColumnValue(row, amountAliases));
                var userVal = GetColumnValue(row, userAliases)?.Trim();
                var dateVal = ParseDateTime(GetColumnValue(row, dateAliases));

                // Resolve User for row
                User? rowUser = null;
                if (!string.IsNullOrWhiteSpace(userVal))
                {
                    var userKey = userVal.ToLowerInvariant();
                    rowUser = usersByEmail[userKey].FirstOrDefault()
                              ?? usersByMatricula[userKey].FirstOrDefault()
                              ?? usersByName[userKey].FirstOrDefault();

                    if (rowUser == null && int.TryParse(userVal, out int parsedInternalId))
                    {
                        rowUser = usersByInternalId[parsedInternalId].FirstOrDefault();
                    }
                }

                // Unassigned / Unmatched User check
                if (rowUser == null && !string.IsNullOrWhiteSpace(userVal))
                {
                    unassignedUserContracts.Add(new ReconciledContractItemDto
                    {
                        ContractNumber = contractNum,
                        TotalAmount = amountVal,
                        UserIdentifier = userVal,
                        SystemUserName = null,
                        Date = dateVal,
                        Source = "XLSX"
                    });
                    continue; // Skip further matching if explicitly assigned to unknown user
                }

                // If user filter is applied (targetUser != null):
                // If row has no user specified, assume it belongs to targetUser.
                // If row has user specified and it matches targetUser, process it.
                // If row has user specified and it matches a DIFFERENT user, ignore for targetUser audit.
                if (targetUser != null)
                {
                    if (rowUser != null && rowUser.Id != targetUser.Id)
                    {
                        // Belongs to another user, skip for Target User scope
                        continue;
                    }
                }
                else if (activeTeamMemberInternalIds != null)
                {
                    if (rowUser != null && !activeTeamMemberInternalIds.Contains(rowUser.InternalId))
                    {
                        // Belongs to a user outside the target Team, skip for Target Team scope
                        continue;
                    }
                }

                var resolvedUserName = rowUser?.Name ?? targetUser?.Name ?? (string.IsNullOrWhiteSpace(userVal) ? null : userVal);

                if (systemContractsMap.TryGetValue(contractNum, out var systemContract))
                {
                    matchedSystemContractNumbers.Add(contractNum);
                    var sysUser = systemContract.UserInternalId.HasValue && userIdToUserMap.TryGetValue(systemContract.UserInternalId.Value, out var u) ? u.Name : resolvedUserName;

                    // Check amount mismatch
                    if (Math.Abs(systemContract.TotalAmount - amountVal) > 0.01m)
                    {
                        amountMismatches.Add(new AmountMismatchItemDto
                        {
                            ContractNumber = contractNum,
                            SystemAmount = systemContract.TotalAmount,
                            XlsxAmount = amountVal,
                            UserIdentifier = userVal ?? resolvedUserName,
                            SystemUserName = sysUser,
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
                            SystemUserName = sysUser
                        });
                    }

                    // Check seller mismatch (when XLSX resolved a user and it differs from system user)
                    if (rowUser != null && (!systemContract.UserInternalId.HasValue || systemContract.UserInternalId.Value != rowUser.InternalId))
                    {
                        sellerMismatches.Add(new SellerMismatchItemDto
                        {
                            ContractNumber = contractNum,
                            TotalAmount = systemContract.TotalAmount,
                            SystemUserName = sysUser,
                            XlsxUserIdentifier = rowUser.Name ?? userVal,
                            SaleStartDate = systemContract.SaleStartDate
                        });
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

            // Contracts in System but NOT in XLSX
            foreach (var sc in systemContracts)
            {
                if (!string.IsNullOrWhiteSpace(sc.ContractNumber) && !matchedSystemContractNumbers.Contains(sc.ContractNumber.Trim()))
                {
                    var sysUser = sc.UserInternalId.HasValue && userIdToUserMap.TryGetValue(sc.UserInternalId.Value, out var u) ? u.Name : targetUser?.Name;

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
                UnassignedUserContracts = unassignedUserContracts
            };

            return Ok(result);
        }

        private static string? GetColumnValue(Dictionary<string, string> row, string[] aliases)
        {
            foreach (var kvp in row)
            {
                var normalizedKey = kvp.Key.Trim().ToLowerInvariant()
                    .Replace("_", "")
                    .Replace("-", "")
                    .Replace(" ", "");

                foreach (var alias in aliases)
                {
                    var normalizedAlias = alias.Replace("_", "").Replace("-", "").Replace(" ", "");
                    if (normalizedKey == normalizedAlias || normalizedKey.Contains(normalizedAlias))
                    {
                        return kvp.Value;
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

            // Support Brazilian currency format: 1.250,50 -> 1250.50
            if (clean.Contains(",") && clean.Contains("."))
            {
                clean = clean.Replace(".", "").Replace(",", ".");
            }
            else if (clean.Contains(","))
            {
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

            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            if (DateTime.TryParse(rawValue, new CultureInfo("pt-BR"), DateTimeStyles.None, out var dtPt))
                return dtPt;

            return null;
        }
    }
}
