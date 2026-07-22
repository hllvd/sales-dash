using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using System.Globalization;
using System.Text.Json;
using OfficeOpenXml;

namespace SalesApp.Services
{
    public class WizardService : IWizardService
    {
        private readonly IImportSessionRepository _sessionRepository;
        private readonly IImportTemplateRepository _templateRepository;
        private readonly IFileParserService _fileParser;
        private readonly IAutoMappingService _autoMapping;
        private readonly IImportExecutionService _importExecution;
        private readonly IUserRepository _userRepository;
        private readonly IWizardHeaderValidator _headerValidator;
        private readonly AppDbContext _context;

        public WizardService(
            IImportSessionRepository sessionRepository,
            IImportTemplateRepository templateRepository,
            IFileParserService fileParser,
            IAutoMappingService autoMapping,
            IImportExecutionService importExecution,
            IUserRepository userRepository,
            IWizardHeaderValidator headerValidator,
            AppDbContext context)
        {
            _sessionRepository = sessionRepository;
            _templateRepository = templateRepository;
            _fileParser = fileParser;
            _autoMapping = autoMapping;
            _importExecution = importExecution;
            _userRepository = userRepository;
            _headerValidator = headerValidator;
            _context = context;
        }

        public async Task<ImportPreviewResponse> ProcessStep1UploadAsync(IFormFile file, Guid userId)
        {
            var fileType = _fileParser.GetFileType(file);
            var uploadId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";

            // Look up user to get InternalId for FK assignment
            var uploadUser = await _userRepository.GetByIdAsync(userId);
            var uploadUserInternalId = uploadUser?.InternalId ?? 0;

            // Try to find the contractDashboard template to link the session
            var template = await _templateRepository.GetByNameAsync("contractDashboard");
            int? templateId = template?.Id;

            // Create import session (initially without rows)
            var session = new ImportSession
            {
                UploadId = uploadId,
                TemplateId = templateId,
                FileName = file.FileName,
                FileType = fileType,
                UploadedByUserInternalId = uploadUserInternalId,
                Status = "wizard_step1",
                TotalRows = 0
            };

            await _sessionRepository.CreateAsync(session);

            // Stream parse file and save rows in batches to avoid OOM
            var allRowsForPreview = new List<Dictionary<string, string>>();
            var columns = await _fileParser.GetColumnsAsync(file);
            
            // Inject virtual columns for dashboard
            var virtualCols = new List<string> { "cota.group", "cota.cota", "cota.customer", "cota.contract" };
            foreach (var col in virtualCols)
            {
                if (!columns.Contains(col)) columns.Add(col);
            }

            var batch = new List<ImportRow>();
            int rowIndex = 0;
            bool foundAtLeastOneValidRow = false;

            // Duplicate contract number detection
            var contractNumberSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var desistenteContracts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileSellers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileMatriculas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int blankContractCount = 0;
            var shortContractNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Outlier amount detection: collect unambiguous totals (for median) and ambiguous ones
            var unambiguousTotals = new List<decimal>();
            var ambiguousRawTotals = new List<(int RowNumber, string RawValue)>();

            await foreach (var row in _fileParser.ParseFileStreamedAsync(file))
            {
                // Inject virtual columns logic
                foreach (var col in virtualCols)
                {
                    if (!row.ContainsKey(col)) row[col] = "";
                }

                var cotaKey = row.Keys.FirstOrDefault(k => k.Equals("Cota", StringComparison.OrdinalIgnoreCase));
                if (cotaKey != null && !string.IsNullOrWhiteSpace(row[cotaKey]))
                {
                    var parts = row[cotaKey].Split(';');
                    if (parts.Length >= 5)
                    {
                        row["cota.group"] = parts[0].Trim();
                        row["cota.cota"] = parts[1].Trim();
                        row["cota.customer"] = parts[3].Trim();
                        row["cota.contract"] = parts[^1].Trim();
                    }
                }

                var nameVal = GetColumnValue(row, "Consultor", "Vendedor", "Comissionado", "Name", "Nome", "Usuário");
                var matVal = GetColumnValue(row, "Matrícula", "Matricula", "Mat", "ID");

                if (!string.IsNullOrWhiteSpace(nameVal)) fileSellers.Add(nameVal.Trim());
                if (!string.IsNullOrWhiteSpace(matVal)) fileMatriculas.Add(matVal.Trim());

                // Check if this row is "valid" for user extraction (has both name and matricula candidates)
                if (!foundAtLeastOneValidRow)
                {
                    if (!string.IsNullOrEmpty(nameVal) && !string.IsNullOrEmpty(matVal))
                    {
                        foundAtLeastOneValidRow = true;
                    }
                }

                // Track contract numbers for duplicate detection
                var contractNumberVal = GetColumnValue(row, "Contrato", "Cota", "cota.contract", "ContractNumber");
                if (!string.IsNullOrWhiteSpace(contractNumberVal))
                {
                    contractNumberSeen.TryGetValue(contractNumberVal, out var count);
                    contractNumberSeen[contractNumberVal] = count + 1;
                }

                // Track desistente contract numbers
                var statusVal = GetColumnValue(row, "Status", "Conferência", "conferencia", "Situação Cobrança", "Situação", "Situacao");
                if (!string.IsNullOrWhiteSpace(statusVal) && statusVal.Trim().Equals("DESISTENTE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(contractNumberVal))
                    {
                        desistenteContracts.Add(contractNumberVal);
                    }
                }

                // pre-validate Contrato field specifically (blank or short <= 3 length)
                var contratoKey = row.Keys.FirstOrDefault(k => k.Equals("Contrato", StringComparison.OrdinalIgnoreCase));
                if (contratoKey != null)
                {
                    var contratoVal = row[contratoKey];
                    if (string.IsNullOrWhiteSpace(contratoVal))
                    {
                        blankContractCount++;
                    }
                    else
                    {
                        var trimmed = contratoVal.Trim();
                        if (trimmed.Length <= 3)
                        {
                            shortContractNumbers.Add(trimmed);
                        }
                    }
                }

                // Collect raw Total values for outlier detection
                var totalRawKey = row.Keys.FirstOrDefault(k =>
                    k.Equals("Total", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("Valor", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("Crédito Venda", StringComparison.OrdinalIgnoreCase));
                if (totalRawKey != null && row.TryGetValue(totalRawKey, out var rawTotalVal) && !string.IsNullOrWhiteSpace(rawTotalVal))
                {
                    var cleaned = rawTotalVal.Trim().Replace("R$", "").Replace("$", "").Trim();
                    int dotCount = cleaned.Count(c => c == '.');
                    int commaCount = cleaned.Count(c => c == ',');

                    if (dotCount >= 2 && commaCount == 0)
                    {
                        // Ambiguous: multiple dots, no comma (e.g. 80.000.00 or 1.000.000.00)
                        ambiguousRawTotals.Add((rowIndex + 1, rawTotalVal.Trim()));
                    }
                    else
                    {
                        // Unambiguous: try parse normally to build median baseline
                        if (TryParseUnambiguousCurrency(cleaned, dotCount, commaCount, out var unambigValue))
                        {
                            unambiguousTotals.Add(unambigValue);
                        }
                    }
                }

                // Keep first 10 rows for preview response
                if (rowIndex < 10) allRowsForPreview.Add(new Dictionary<string, string>(row));

                batch.Add(new ImportRow
                {
                    ImportSessionId = session.Id,
                    RowIndex = rowIndex,
                    RowData = JsonSerializer.Serialize(row)
                });

                rowIndex++;

                if (batch.Count >= 500)
                {
                    await _context.ImportRows.AddRangeAsync(batch);
                    await _context.SaveChangesAsync();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _context.ImportRows.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            // Update session total rows
            session.TotalRows = rowIndex;
            await _sessionRepository.UpdateAsync(session);

            // Strict Header Validation (User requested)
            var validationResult = _headerValidator.Validate(columns);
            var isTemplateMatch = validationResult.IsValid;
            string? matchMessage = null;

            if (!isTemplateMatch)
            {
                var missingList = string.Join(", ", validationResult.MissingHeaders);
                var expectedList = string.Join(", ", validationResult.ExpectedHeaders);
                matchMessage = $"Atenção: O arquivo não possui todos os cabeçalhos esperados. Colunas ausentes: {missingList}. Esperamos as seguintes colunas como cabeçalho: {expectedList}.";
            }

            Dictionary<string, string> suggestedMappings = new();
            List<string> requiredFields = new();
            List<string> optionalFields = new();

            if (template != null)
            {
                requiredFields = JsonSerializer.Deserialize<List<string>>(template.RequiredFields) ?? new();
                optionalFields = JsonSerializer.Deserialize<List<string>>(template.OptionalFields) ?? new();
                
                var allTemplateFields = new List<string>();
                allTemplateFields.AddRange(requiredFields);
                allTemplateFields.AddRange(optionalFields);

                suggestedMappings = _autoMapping.SuggestMappings(columns, template.EntityType, allTemplateFields);
                
                if (!string.IsNullOrEmpty(template.DefaultMappings) && template.DefaultMappings != "{}")
                {
                    var templateMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(template.DefaultMappings) ?? new();
                    var appliedMappings = _autoMapping.ApplyTemplateMappings(templateMappings, columns);
                    
                    foreach (var (src, target) in appliedMappings)
                    {
                        suggestedMappings[src] = target;
                    }
                }
                
                if (template.Name == "contractDashboard")
                {
                    // Ensure virtual columns are mapped to their corresponding fields
                    if (columns.Contains("cota.cota")) suggestedMappings["cota.cota"] = "Quota";
                    if (columns.Contains("cota.contract")) suggestedMappings["cota.contract"] = "ContractNumber";
                    if (columns.Contains("cota.group")) suggestedMappings["cota.group"] = "GroupId";
                    if (columns.Contains("cota.customer")) suggestedMappings["cota.customer"] = "CustomerName";
                    
                    // Remove "Cota" from automapping to prevent conflict (User request)
                    var cotaSource = suggestedMappings.FirstOrDefault(m => string.Equals(m.Key, "Cota", StringComparison.OrdinalIgnoreCase)).Key;
                    if (cotaSource != null && suggestedMappings[cotaSource] == "Quota")
                    {
                        suggestedMappings.Remove(cotaSource);
                    }
                }
            }

            var duplicateContractNumbers = contractNumberSeen
                .Where(kv => kv.Value > 1)
                .Select(kv => kv.Key)
                .OrderBy(n => n)
                .ToList();

            var desistenteContractNumbers = desistenteContracts
                .OrderBy(n => n)
                .ToList();

            // ── Outlier Amount Detection (median-based) ────────────────────────
            var outlierAmounts = new List<OutlierAmountEntry>();
            if (ambiguousRawTotals.Count > 0)
            {
                decimal fileMedian = 0m;
                if (unambiguousTotals.Count > 0)
                {
                    var sorted = unambiguousTotals.OrderBy(v => v).ToList();
                    int mid = sorted.Count / 2;
                    fileMedian = sorted.Count % 2 == 0
                        ? (sorted[mid - 1] + sorted[mid]) / 2m
                        : sorted[mid];
                }

                foreach (var (rowNum, raw) in ambiguousRawTotals.Take(50))
                {
                    var cleaned = raw.Replace("R$", "").Replace("$", "").Trim();
                    var parts = cleaned.Split('.');

                    // Interpretation A: treat last dot as decimal separator (e.g. 80.000.00 → 80000.00)
                    decimal interpA = 0m;
                    var asDecimalStr = string.Join("", parts[..^1]) + "." + parts[^1];
                    decimal.TryParse(asDecimalStr, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out interpA);

                    // Interpretation B: all dots are thousand separators (e.g. 80.000.00 → 8000000)
                    decimal interpB = 0m;
                    var asThousandsStr = cleaned.Replace(".", "");
                    decimal.TryParse(asThousandsStr, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out interpB);

                    if (interpA <= 0 && interpB <= 0) continue;

                    // Pick the interpretation closest to the file median as "likely"
                    decimal likelyValue, altValue;
                    if (fileMedian > 0)
                    {
                        var distA = Math.Abs(interpA - fileMedian);
                        var distB = Math.Abs(interpB - fileMedian);
                        likelyValue = distA <= distB ? interpA : interpB;
                        altValue   = distA <= distB ? interpB : interpA;
                    }
                    else
                    {
                        // No baseline: default to smaller value (less likely to be catastrophically wrong)
                        likelyValue = interpA < interpB ? interpA : interpB;
                        altValue   = interpA < interpB ? interpB : interpA;
                    }

                    outlierAmounts.Add(new OutlierAmountEntry(
                        RowNumber: rowNum,
                        RawValue: raw,
                        LikelyValue: likelyValue,
                        AltValue: altValue,
                        LikelyFormatted: likelyValue.ToString("C2", new System.Globalization.CultureInfo("pt-BR")),
                        AltFormatted: altValue.ToString("C2", new System.Globalization.CultureInfo("pt-BR")),
                        FileMedian: fileMedian
                    ));
                }
            }

            // ── Inconsistency Detection (Pre-Import Warning) ────────────────────
            var conflictingUserNames = new List<string>();
            var conflictingMatriculas = new List<string>();

            if (fileSellers.Count > 0 || fileMatriculas.Count > 0)
            {
                var activeUsers = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.UserMatriculas)
                        .ThenInclude(um => um.Matricula)
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.InternalId)
                    .ToListAsync();

                // 1. Ambiguous active user names in file
                var nameGroups = activeUsers
                    .GroupBy(u => u.Name.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var seller in fileSellers)
                {
                    if (nameGroups.TryGetValue(seller, out var matchedUsers) && matchedUsers.Count > 1)
                    {
                        var emails = string.Join(" e ", matchedUsers.Select(u => u.Email));
                        conflictingUserNames.Add($"O vendedor '{seller}' possui múltiplos e-mails ativos: {emails}");
                    }
                }

                // 2. Matriculas with multiple active users/owners
                var matriculaGroups = activeUsers
                    .SelectMany(u => u.UserMatriculas
                        .Where(um => um.IsActive)
                        .Select(um => new { User = u, MatriculaNumber = um.Matricula.MatriculaNumber.Trim() }))
                    .GroupBy(x => x.MatriculaNumber)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var mat in fileMatriculas)
                {
                    if (matriculaGroups.TryGetValue(mat, out var matchedMatriculas) && matchedMatriculas.Count > 1)
                    {
                        var userDetails = string.Join(" | ", matchedMatriculas.Select(x => $"{x.User.Name} ({x.User.Email})"));
                        conflictingMatriculas.Add($"A matrícula '{mat}' está associada a múltiplos usuários ativos: {userDetails}");
                    }
                }
            }

            return new ImportPreviewResponse
            {
                UploadId = uploadId,
                SessionId = uploadId,
                TemplateId = templateId ?? 0,
                TemplateName = "contractDashboard",
                EntityType = "Contract",
                FileName = file.FileName,
                DetectedColumns = columns,
                SampleRows = allRowsForPreview.Take(5).ToList(),
                TotalRows = rowIndex,
                IsTemplateMatch = isTemplateMatch,
                MatchMessage = matchMessage,
                SuggestedMappings = suggestedMappings,
                RequiredFields = requiredFields,
                OptionalFields = optionalFields,
                DuplicateContractNumbers = duplicateContractNumbers,
                DesistenteContractNumbers = desistenteContractNumbers,
                ConflictingUserNames = conflictingUserNames,
                ConflictingMatriculas = conflictingMatriculas,
                BlankContractCount = blankContractCount,
                ShortContractNumbers = shortContractNumbers.OrderBy(n => n).ToList(),
                OutlierAmounts = outlierAmounts
            };
        }

        public async Task<byte[]> GenerateUsersTemplateAsync(string uploadId)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                throw new ArgumentException("Session not found");
            }

            var userMap = new HashSet<(string Name, string Matricula)>();

            // Fetch rows chunked from DB instead of memory
            int skip = 0;
            int take = 500;
            while (true)
            {
                var rowBatch = await _context.ImportRows
                    .Where(r => r.ImportSessionId == session.Id)
                    .OrderBy(r => r.RowIndex)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                if (rowBatch.Count == 0) break;

                foreach (var dbRow in rowBatch)
                {
                    var row = JsonSerializer.Deserialize<Dictionary<string, string>>(dbRow.RowData) ?? new();
                    var nameVal = GetColumnValue(row, "Consultor", "Vendedor", "Comissionado", "Name", "name", "Nome", "Usuário");
                    var matVal = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");

                    if (!string.IsNullOrEmpty(nameVal) && !string.IsNullOrEmpty(matVal))
                    {
                        userMap.Add((nameVal.Trim(), matVal.Trim()));
                    }
                }

                skip += take;
            }
            
            using var memoryStream = new MemoryStream();
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using (var package = new ExcelPackage(memoryStream))
            {
                var worksheet = package.Workbook.Worksheets.Add("Users");

                worksheet.Cells[1, 1].Value = "Name";
                worksheet.Cells[1, 2].Value = "Email";
                worksheet.Cells[1, 3].Value = "ParentEmail";
                worksheet.Cells[1, 4].Value = "Matricula";
                worksheet.Cells[1, 5].Value = "Owner_Matricula";
                worksheet.Cells[1, 6].Value = "Password";

                int row = 2;
                foreach (var user in userMap.OrderBy(u => u.Name))
                {
                    worksheet.Cells[row, 1].Value = user.Name;
                    worksheet.Cells[row, 4].Value = user.Matricula;
                    worksheet.Cells[row, 5].Value = "0";
                    row++;
                }

                package.Save();
            }

            return memoryStream.ToArray();
        }

        public async Task<ImportStatusResponse> ProcessStep2ImportAsync(string uploadId, IFormFile usersFile, Guid userId)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                throw new ArgumentException("Original session not found");
            }

            var userRows = await _fileParser.ParseFileAsync(usersFile);

            // Check for duplicate emails with different user names (case-insensitive)
            var emailToNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in userRows)
            {
                var nameVal = GetColumnValue(row, "Name", "name", "Nome", "Usuário")?.Trim();
                var emailVal = GetColumnValue(row, "Email", "email", "E-mail")?.Trim()?.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(emailVal) || string.IsNullOrWhiteSpace(nameVal))
                {
                    continue;
                }

                if (!emailToNames.TryGetValue(emailVal, out var names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    emailToNames[emailVal] = names;
                }
                names.Add(nameVal);
            }

            var duplicateEmailErrors = new List<string>();
            foreach (var kvp in emailToNames)
            {
                if (kvp.Value.Count > 1)
                {
                    var sortedNames = kvp.Value.OrderBy(n => n).ToList();
                    duplicateEmailErrors.Add($"O e-mail '{kvp.Key}' está associado a múltiplos usuários: {string.Join(", ", sortedNames)}.");
                }
            }

            if (duplicateEmailErrors.Any())
            {
                throw new ArgumentException(string.Join("\n", duplicateEmailErrors));
            }
            
            var userMappings = new Dictionary<string, string>
            {
                ["Name"] = "Name",
                ["Email"] = "Email",
                ["ParentEmail"] = "ParentEmail",
                ["Matricula"] = "Matricula",
                ["Owner_Matricula"] = "IsMatriculaOwner",
                ["Password"] = "Password"
            };

            var userResult = await _importExecution.ExecuteUserImportAsync(
                uploadId,
                session.Id,
                userRows,
                userMappings
            );

            session.Status = userResult.FailedRows > 0 ? "completed_with_errors" : "completed";
            session.CompletedAt = DateTime.UtcNow;
            session.ProcessedRows = userResult.ProcessedRows;
            session.FailedRows = userResult.FailedRows;
            
            var usersTemplate = await _templateRepository.GetByNameAsync("Users");
            session.TemplateId = usersTemplate?.Id;
            
            await _sessionRepository.UpdateAsync(session);

            return new ImportStatusResponse
            {
                UploadId = uploadId,
                Status = session.Status,
                TotalRows = userRows.Count,
                ProcessedRows = userResult.ProcessedRows,
                FailedRows = userResult.FailedRows,
                Errors = userResult.Errors,
                FailedRowsDetails = userResult.FailedRowsDetails
            };
        }

        public async Task<byte[]> GenerateEnrichedContractsAsync(string uploadId, Guid userId)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                throw new ArgumentException("Session not found");
            }

            // Get column list from first row
            var firstDbRow = await _context.ImportRows
                .Where(r => r.ImportSessionId == session.Id)
                .OrderBy(r => r.RowIndex)
                .FirstOrDefaultAsync();

            if (firstDbRow == null) return Array.Empty<byte>();

            var firstRow = JsonSerializer.Deserialize<Dictionary<string, string>>(firstDbRow.RowData) ?? new();
            var columns = firstRow.Keys.ToList();
            
            var allActiveUsers = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .Where(u => u.IsActive)
                .OrderBy(u => u.InternalId) // Force deterministic ordering
                .ToListAsync();

            var exactMatchLookup = new Dictionary<(string mat, string name), string>();
            var nameLookup = new Dictionary<string, string>();
            var matriculaOwnerLookup = new Dictionary<string, string>();
            var matriculaAnyLookup = new Dictionary<string, string>();
            var ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ambiguousMatriculaOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ambiguousMatriculas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var u in allActiveUsers)
            {
                var normalizedName = u.Name.ToLower().Trim();
                if (nameLookup.ContainsKey(normalizedName))
                {
                    ambiguousNames.Add(normalizedName);
                }
                else
                {
                    nameLookup[normalizedName] = u.Email;
                }

                foreach (var m in u.UserMatriculas.Where(m => m.IsActive))
                {
                    var normalizedMat = m.Matricula.MatriculaNumber.ToLower().Trim();
                    exactMatchLookup[(normalizedMat, normalizedName)] = u.Email;
                    
                    if (m.IsOwner) {
                        if (matriculaOwnerLookup.ContainsKey(normalizedMat))
                        {
                            ambiguousMatriculaOwners.Add(normalizedMat);
                        }
                        else
                        {
                            matriculaOwnerLookup[normalizedMat] = u.Email;
                        }
                    }
                    
                    if (matriculaAnyLookup.ContainsKey(normalizedMat))
                    {
                        ambiguousMatriculas.Add(normalizedMat);
                    }
                    else
                    {
                        matriculaAnyLookup[normalizedMat] = u.Email;
                    }
                }
            }

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Contratos");
            int excelRow = 1;

            // Write headers
            var filteredColumns = columns
                .Where(c => !string.IsNullOrWhiteSpace(c) && !c.StartsWith("Column_", StringComparison.OrdinalIgnoreCase))
                .Where(c => !c.StartsWith("cota.", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int col = 1;
            foreach (var header in filteredColumns)
            {
                worksheet.Cells[excelRow, col].Value = header;
                col++;
            }
            worksheet.Cells[excelRow, col].Value = "Email";
            excelRow++;

            // Write data rows in batches
            int skip = 0;
            int take = 500;
            while (true)
            {
                var rowBatch = await _context.ImportRows
                    .Where(r => r.ImportSessionId == session.Id)
                    .OrderBy(r => r.RowIndex)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                if (rowBatch.Count == 0) break;

                foreach (var dbRow in rowBatch)
                {
                    var row = JsonSerializer.Deserialize<Dictionary<string, string>>(dbRow.RowData) ?? new();

                    var nameVal = GetColumnValue(row, "Consultor", "Vendedor", "Comissionado", "Name", "name", "Nome", "Usuário");
                    var matVal = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");

                    var nameNorm = nameVal.ToLower().Trim();
                    var matNorm = matVal.ToLower().Trim();

                    string? email = null;
                    if (!string.IsNullOrEmpty(matNorm) && !string.IsNullOrEmpty(nameNorm) && exactMatchLookup.TryGetValue((matNorm, nameNorm), out var exactEmail))
                    {
                        email = exactEmail;
                    }
                    else if (!string.IsNullOrEmpty(nameNorm) && !ambiguousNames.Contains(nameNorm) && nameLookup.TryGetValue(nameNorm, out var nameEmail))
                    {
                        email = nameEmail;
                    }
                    else if (!string.IsNullOrEmpty(matNorm))
                    {
                        if (!ambiguousMatriculaOwners.Contains(matNorm) && matriculaOwnerLookup.TryGetValue(matNorm, out var ownerEmail)) {
                            email = ownerEmail;
                        } else if (!ambiguousMatriculas.Contains(matNorm) && matriculaAnyLookup.TryGetValue(matNorm, out var anyEmail)) {
                            email = anyEmail;
                        }
                    }

                    var confKey = row.Keys.FirstOrDefault(k => k.Equals("Conferência", StringComparison.OrdinalIgnoreCase) || k.Equals("conferencia", StringComparison.OrdinalIgnoreCase));
                    var statusKey = row.Keys.FirstOrDefault(k => k.Equals("Status", StringComparison.OrdinalIgnoreCase));

                    if (confKey != null)
                    {
                        var statusValue = MapConferenciaToStatus(row[confKey]);
                        if (statusKey != null) row[statusKey] = statusValue;
                        else row["Status"] = statusValue;
                    }

                    col = 1;
                    foreach (var header in filteredColumns)
                    {
                        var val = row.TryGetValue(header, out var cellVal) ? cellVal : string.Empty;
                        if (header.Contains("Data", StringComparison.OrdinalIgnoreCase) || header.Contains("Dt", StringComparison.OrdinalIgnoreCase))
                        {
                            if (DateTime.TryParse(val, out var dt))
                            {
                                worksheet.Cells[excelRow, col].Value = dt.ToString("MM/dd/yyyy");
                                col++;
                                continue;
                            }
                            else if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double oaDate))
                            {
                                try
                                {
                                    worksheet.Cells[excelRow, col].Value = DateTime.FromOADate(oaDate).ToString("MM/dd/yyyy");
                                    col++;
                                    continue;
                                }
                                catch { }
                            }
                        }
                        worksheet.Cells[excelRow, col].Value = val;
                        col++;
                    }

                    worksheet.Cells[excelRow, col].Value = email ?? string.Empty;
                    excelRow++;
                }

                skip += take;
            }

            var xlsxBytes = package.GetAsByteArray();

            // ── Persist to temp folder for audit and later direct import ─────────
            var tempDir = GetTempDirectory();
            var tempFileName = $"{userId}_{uploadId}.xlsx";
            var tempFilePath = Path.Combine(tempDir, tempFileName);
            await File.WriteAllBytesAsync(tempFilePath, xlsxBytes);
            Console.WriteLine($"[Wizard] Step 3: Temp file saved to {tempFilePath}");

            return xlsxBytes;
        }

        public async Task<ImportStatusResponse> ImportWizardContractsAsync(string uploadId, Guid userId, WizardContractImportOptions options)
        {
            // ── Locate temp file ─────────────────────────────────────────────────
            var tempDir = GetTempDirectory();
            var tempFileName = $"{userId}_{uploadId}.xlsx";
            var tempFilePath = Path.Combine(tempDir, tempFileName);

            if (!File.Exists(tempFilePath))
            {
                throw new FileNotFoundException($"Temp file not found. Please download the contracts file first: {tempFileName}");
            }

            // ── Parse the xlsx using EPPlus ──────────────────────────────────────
            var rows = new List<Dictionary<string, string>>();
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using (var package = new ExcelPackage(new FileInfo(tempFilePath)))
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("No worksheet found in temp file.");

                int colCount = worksheet.Dimension?.Columns ?? 0;
                int rowCount = worksheet.Dimension?.Rows ?? 0;
                if (colCount == 0 || rowCount < 2)
                    throw new InvalidOperationException("Temp file is empty or has no data rows.");

                // Build header list from row 1
                var headers = Enumerable.Range(1, colCount)
                    .Select(c => worksheet.Cells[1, c].Text?.Trim() ?? string.Empty)
                    .ToList();

                // Parse data rows
                for (int r = 2; r <= rowCount; r++)
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 1; c <= colCount; c++)
                    {
                        var header = headers[c - 1];
                        if (!string.IsNullOrEmpty(header))
                            row[header] = worksheet.Cells[r, c].Text?.Trim() ?? string.Empty;
                    }
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("No data rows found in temp file.");

            // ── Build Contracts template mappings (templateId=2) ─────────────────
            // Column names come from the actual enriched xlsx headers (Portuguese).
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Email"]            = "UserEmail",
                ["Matrícula"]        = "MatriculaNumber",
                ["Matricula"]        = "MatriculaNumber",
                ["Contrato"]         = "ContractNumber",
                ["Cota"]             = "ContractNumber",
                ["Valor"]            = "TotalAmount",
                ["Crédito Venda"]    = "TotalAmount",
                ["Grupo"]            = "GroupId",
                ["Cota_Number"]      = "Quota", // Alias to avoid conflict with the concatenated 'Cota' column
                ["Data da Venda"]    = "SaleStartDate",
                ["Dt Venda"]         = "SaleStartDate",
                ["Dt Produção"]      = "SaleStartDate",
                ["Nome do Cliente"]  = "CustomerName",
                ["Consultor"]        = "CustomerName", // Fallback for name if needed
                ["Código PV"]        = "PvId",
                ["Cód. PV"]          = "PvId",
                ["Nome PV"]          = "PvName",
                ["PV"]               = "PvName",
                ["Tipo"]             = "ContractType",
                ["Status"]           = "Status",
                ["Situação Cobrança"] = "Status",
                ["Versao"]           = "Version",
                ["Versão"]           = "Version",
            };

            // Intersect with actual headers so we only pass valid mappings
            var firstRow = rows[0];
            var activeMappings = mappings
                .Where(kv => firstRow.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            Console.WriteLine($"[Wizard] Step3 Import: {rows.Count} rows. Active mappings: {string.Join(", ", activeMappings.Select(kv => $"{kv.Key}->{kv.Value}"))}");

            // ── Create a new import session for audit trail ──────────────────────
            var contractsTemplate = await _templateRepository.GetByNameAsync("Contracts");
            int? contractsTemplateId = contractsTemplate?.Id;

            var wizardImportUploadId = $"wiz-{uploadId}-ctr-{DateTime.UtcNow:HHmmss}";
            var wizardUser = await _userRepository.GetByIdAsync(userId);
            var importSession = new ImportSession
            {
                UploadId = wizardImportUploadId,
                TemplateId = contractsTemplateId,
                FileName = $"{userId}_{uploadId}.xlsx",
                FileType = "xlsx",
                UploadedByUserInternalId = wizardUser?.InternalId ?? 0,
                Status = "wizard_step3",
                TotalRows = rows.Count
            };
            await _sessionRepository.CreateAsync(importSession);

            // ── Pre-import contract count snapshot ────────────────────────────────
            var fileEmails = rows
                .Select(r => r.TryGetValue("Email", out var emailVal) ? emailVal?.Trim() : null)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            var affectedUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && fileEmails.Contains(u.Email))
                .ToListAsync();
            var affectedUserIds = affectedUsers.Select(u => u.InternalId).ToList();

            var preImportCounts = await _context.Contracts
                .AsNoTracking()
                .Where(c => affectedUserIds.Contains(c.UserInternalId ?? 0))
                .GroupBy(c => c.UserInternalId)
                .Select(g => new { UserInternalId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserInternalId ?? 0, x => x.Count);

            // ── Execute import in batches ────────────────────────────────────────
            var totalResult = new ImportResult();

            // ── Fallback/Inconsistency Warnings Detection ────────────────────────
            var activeUsers = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .Where(u => u.IsActive)
                .OrderBy(u => u.InternalId)
                .ToListAsync();

            var usersByEmail = activeUsers.ToDictionary(u => u.Email, u => u, StringComparer.OrdinalIgnoreCase);
            var nameGroups = activeUsers.GroupBy(u => u.Name.Trim().ToLower()).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var matriculaGroups = activeUsers
                .SelectMany(u => u.UserMatriculas.Where(um => um.IsActive).Select(um => new { User = u, Mat = um.Matricula.MatriculaNumber.Trim() }))
                .GroupBy(x => x.Mat)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var fallbackWarnings = new HashSet<string>();

            foreach (var row in rows)
            {
                var emailVal = row.TryGetValue("Email", out var e) ? e?.Trim() : null;
                var nameVal = GetColumnValue(row, "Consultor", "Vendedor", "Comissionado", "Name", "name", "Nome", "Usuário");
                var matVal = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");

                var nameNorm = nameVal?.Trim().ToLower();
                var matNorm = matVal?.Trim().ToLower();

                if (!string.IsNullOrEmpty(emailVal))
                {
                    if (usersByEmail.TryGetValue(emailVal, out var user))
                    {
                        var userMatriculas = user.UserMatriculas.Where(m => m.IsActive).Select(m => m.Matricula.MatriculaNumber.ToLower().Trim()).ToList();
                        var userNameNorm = user.Name.ToLower().Trim();

                        if (!string.IsNullOrEmpty(nameNorm) && userNameNorm != nameNorm)
                        {
                            if (nameGroups.TryGetValue(nameNorm, out var dupUsers) && dupUsers.Count > 1)
                            {
                                fallbackWarnings.Add($"O contrato do consultor '{nameVal}' foi associado a '{user.Name}' ({user.Email}) devido a ambiguidade no nome.");
                            }
                            else if (!string.IsNullOrEmpty(matNorm) && !userMatriculas.Contains(matNorm))
                            {
                                fallbackWarnings.Add($"O contrato com matrícula '{matVal}' e nome '{nameVal}' foi associado a '{user.Name}' ({user.Email}) via fallback de matrícula, pois o nome é diferente.");
                            }
                        }

                        if (!string.IsNullOrEmpty(matNorm) && matriculaGroups.TryGetValue(matNorm, out var sharedMats) && sharedMats.Count > 1)
                        {
                            fallbackWarnings.Add($"A matrícula '{matVal}' é compartilhada entre múltiplos usuários ativos. O contrato foi associado a '{user.Name}' ({user.Email}).");
                        }
                    }
                }
            }

            totalResult.Warnings.AddRange(fallbackWarnings);
            int batchSize = 500;
            for (int i = 0; i < rows.Count; i += batchSize)
            {
                var batch = rows.Skip(i).Take(batchSize).ToList();
                var result = await _importExecution.ExecuteContractImportAsync(
                    wizardImportUploadId,
                    importSession.Id,
                    batch,
                    activeMappings,
                    options.DateFormat,
                    options.SkipMissingContractNumber,
                    options.AllowAutoCreateGroups,
                    options.AllowAutoCreatePVs
                );
                totalResult.ProcessedRows += result.ProcessedRows;
                totalResult.FailedRows += result.FailedRows;
                totalResult.Errors.AddRange(result.Errors);
                totalResult.Warnings.AddRange(result.Warnings);
                totalResult.CreatedGroups.AddRange(result.CreatedGroups);
                totalResult.CreatedPVs.AddRange(result.CreatedPVs);
                totalResult.DesistenteContractNumbers.AddRange(result.DesistenteContractNumbers);
                totalResult.FailedRowsDetails.AddRange(result.FailedRowsDetails);
            }

            // ── Post-import contract count snapshot ───────────────────────────────
            var postImportCounts = await _context.Contracts
                .AsNoTracking()
                .Where(c => affectedUserIds.Contains(c.UserInternalId ?? 0))
                .GroupBy(c => c.UserInternalId)
                .Select(g => new { UserInternalId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserInternalId ?? 0, x => x.Count);

            var userDeltas = new List<UserContractCountDelta>();
            foreach (var user in affectedUsers)
            {
                preImportCounts.TryGetValue(user.InternalId, out var beforeCount);
                postImportCounts.TryGetValue(user.InternalId, out var afterCount);
                userDeltas.Add(new UserContractCountDelta
                {
                    UserName = user.Name,
                    Email = user.Email,
                    Before = beforeCount,
                    After = afterCount,
                    Delta = afterCount - beforeCount
                });
            }

            // ── Update session ───────────────────────────────────────────────────
            importSession.Status = totalResult.FailedRows > 0 ? "completed_with_errors" : "completed";
            importSession.CompletedAt = DateTime.UtcNow;
            importSession.ProcessedRows = totalResult.ProcessedRows;
            importSession.FailedRows = totalResult.FailedRows;
            await _sessionRepository.UpdateAsync(importSession);

            return new ImportStatusResponse
            {
                UploadId = wizardImportUploadId,
                Status = importSession.Status,
                TotalRows = rows.Count,
                ProcessedRows = totalResult.ProcessedRows,
                FailedRows = totalResult.FailedRows,
                Errors = totalResult.Errors,
                Warnings = totalResult.Warnings,
                CreatedGroups = totalResult.CreatedGroups.Distinct().ToList(),
                CreatedPVs = totalResult.CreatedPVs.Distinct().ToList(),
                DesistenteContractNumbers = totalResult.DesistenteContractNumbers.Distinct().ToList(),
                FailedRowsDetails = totalResult.FailedRowsDetails,
                UserContractCountDeltas = userDeltas
            };
        }

        private string MapConferenciaToStatus(string conferencia)
        {
            var normalized = conferencia.Trim().ToUpper();
            if (normalized == "DESISTENTE")
            {
                return "DESISTENTE";
            }
            return normalized switch
            {
                "NORMAL" => "Active",
                "NCONT 1 AT" => "Late1",
                "NCONT 2 AT" => "Late2",
                "SUJ. A CANCELAMENTO" => "Late3",
                "EXCLUIDO" => "Defaulted",
                _ => "Active"
            };
        }

        private string GetColumnValue(Dictionary<string, string> row, params string[] options)
        {
            foreach (var opt in options)
            {
                if (row.TryGetValue(opt, out var val) && !string.IsNullOrEmpty(val)) return val;
                
                var key = row.Keys.FirstOrDefault(k => string.Equals(k, opt, StringComparison.OrdinalIgnoreCase));
                if (key != null && !string.IsNullOrEmpty(row[key])) return row[key];
            }
            return string.Empty;
        }

        private bool NameMatchesOrIsSimilar(string rowNameNorm, string userEmail, List<User> activeUsers)
        {
            var user = activeUsers.FirstOrDefault(u => u.Email.Equals(userEmail, StringComparison.OrdinalIgnoreCase));
            if (user == null) return false;
            
            var dbNameNorm = user.Name.ToLower().Trim();
            if (dbNameNorm == rowNameNorm) return true;
            
            var rowFirstWord = rowNameNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var dbFirstWord = dbNameNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            
            if (!string.IsNullOrEmpty(rowFirstWord) && !string.IsNullOrEmpty(dbFirstWord))
            {
                return rowFirstWord == dbFirstWord;
            }
            
            return false;
        }

        private string GetTempDirectory()
        {
            var tempDir = Path.Combine("/app", "wizard-temp");
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                return tempDir;
            }
            catch
            {
                var fallbackDir = Path.Combine(Directory.GetCurrentDirectory(), "wizard-temp");
                if (!Directory.Exists(fallbackDir))
                {
                    Directory.CreateDirectory(fallbackDir);
                }
                return fallbackDir;
            }
        }

        /// <summary>
        /// Parses a pre-cleaned (no currency symbol) numeric string only when its format is unambiguous.
        /// Used to build the median baseline for outlier detection.
        /// Returns false for multi-dot-no-comma patterns (those are the ambiguous ones we flag).
        /// </summary>
        private static bool TryParseUnambiguousCurrency(string cleaned, int dotCount, int commaCount, out decimal result)
        {
            result = 0;

            // Skip patterns that are exactly the ambiguous case handled separately
            if (dotCount >= 2 && commaCount == 0) return false;

            string normalized = cleaned;

            if (dotCount == 1 && commaCount == 1)
            {
                // Both separators: determine which is decimal (last one wins)
                int lastDot   = cleaned.LastIndexOf('.');
                int lastComma = cleaned.LastIndexOf(',');
                normalized = lastComma > lastDot
                    ? cleaned.Replace(".", "").Replace(",", ".")   // BR format: 1.000,00
                    : cleaned.Replace(",", "");                    // US format: 1,000.00
            }
            else if (commaCount == 1 && dotCount == 0)
            {
                int commaIdx = cleaned.IndexOf(',');
                int digitsAfter = cleaned.Length - commaIdx - 1;
                normalized = digitsAfter == 2
                    ? cleaned.Replace(",", ".")   // decimal comma: 100,00
                    : cleaned.Replace(",", "");   // thousand comma: 1,000
            }
            else if (commaCount > 1 && dotCount == 0)
            {
                // Multiple commas → BR thousands+decimal: 1,000,000.00 style — strip commas
                normalized = cleaned.Replace(",", "");
            }
            // else: no separators or only dots with count < 2 — use as-is

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
                   && result > 0;
        }
    }
}
