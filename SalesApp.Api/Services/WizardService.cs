using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using System.Globalization;
using System.Text.Json;

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
        private readonly AppDbContext _context;

        public WizardService(
            IImportSessionRepository sessionRepository,
            IImportTemplateRepository templateRepository,
            IFileParserService fileParser,
            IAutoMappingService autoMapping,
            IImportExecutionService importExecution,
            IUserRepository userRepository,
            AppDbContext context)
        {
            _sessionRepository = sessionRepository;
            _templateRepository = templateRepository;
            _fileParser = fileParser;
            _autoMapping = autoMapping;
            _importExecution = importExecution;
            _userRepository = userRepository;
            _context = context;
        }

        public async Task<ImportPreviewResponse> ProcessStep1UploadAsync(IFormFile file, Guid userId)
        {
            var fileType = _fileParser.GetFileType(file);
            var uploadId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";

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
                UploadedByUserId = userId,
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

            // Template verification logic
            var isTemplateMatch = true;
            string? matchMessage = null;
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
                
                var mappedRequiredFieldsCount = requiredFields.Count(rf => suggestedMappings.Values.Contains(rf));

                if (requiredFields.Any())
                {
                    if (mappedRequiredFieldsCount < (requiredFields.Count + 1) / 2)
                    {
                        isTemplateMatch = false;
                        matchMessage = $"Atenção: O arquivo enviado não parece corresponder ao modelo '{template.Name}'. Foram identificados apenas {mappedRequiredFieldsCount} de {requiredFields.Count} campos obrigatórios.";
                    }
                    else if (template.Name == "contractDashboard" && mappedRequiredFieldsCount < 3)
                    {
                        isTemplateMatch = false;
                        matchMessage = "Atenção: Este arquivo não parece ser um dashboard de contratos válido.";
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
                OptionalFields = optionalFields
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
                    var nameVal = GetColumnValue(row, "Comissionado", "Name", "name", "Nome", "Vendedor", "Usuário");
                    var matVal = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");

                    if (!string.IsNullOrEmpty(nameVal) && !string.IsNullOrEmpty(matVal))
                    {
                        userMap.Add((nameVal.Trim(), matVal.Trim()));
                    }
                }

                skip += take;
            }
            
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.Write('\uFEFF');
                using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csv.WriteField("Name");
                    csv.WriteField("Email");
                    csv.WriteField("ParentEmail");
                    csv.WriteField("Matricula");
                    csv.WriteField("Owner_Matricula");
                    csv.WriteField("Password");
                    csv.NextRecord();

                    foreach (var user in userMap.OrderBy(u => u.Name))
                    {
                        csv.WriteField(user.Name);
                        csv.WriteField(""); 
                        csv.WriteField(""); 
                        csv.WriteField(user.Matricula);
                        csv.WriteField("0");
                        csv.WriteField(""); 
                        csv.NextRecord();
                    }
                }
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
                Errors = userResult.Errors
            };
        }

        public async Task<byte[]> GenerateEnrichedContractsAsync(string uploadId)
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
                .Where(u => u.IsActive)
                .ToListAsync();

            var dbMatriculaLookup = new Dictionary<string, string>();
            var dbNameLookup = new Dictionary<string, string>();

            foreach (var u in allActiveUsers)
            {
                foreach (var m in u.UserMatriculas.Where(m => m.IsActive))
                {
                    dbMatriculaLookup[m.MatriculaNumber.ToLower().Trim()] = u.Email;
                }
                
                var nomalizedName = u.Name.ToLower().Trim();
                if (!dbNameLookup.ContainsKey(nomalizedName))
                {
                    dbNameLookup[nomalizedName] = u.Email;
                }
            }

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.Write('\uFEFF');
                
                using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    var filteredColumns = columns
                        .Where((c, index) => index != 16 && index != 17)
                        .Where(c => !c.StartsWith("cota.", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    foreach (var col in filteredColumns) csv.WriteField(col);
                    csv.WriteField("Email");
                    csv.NextRecord();

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
                            
                            var nameRaw = GetColumnValue(row, "Comissionado", "Name", "name", "Nome", "Vendedor", "Usuário");
                            var matRaw = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");
                            
                            var nameVal = nameRaw.ToLower().Trim();
                            var matVal = matRaw.ToLower().Trim();
                            
                            string? email = null;
                            if (!string.IsNullOrEmpty(matVal) && dbMatriculaLookup.TryGetValue(matVal, out var dbEmailByMat))
                            {
                                email = dbEmailByMat;
                            }
                            else if (!string.IsNullOrEmpty(nameVal) && dbNameLookup.TryGetValue(nameVal, out var dbEmailByName))
                            {
                                email = dbEmailByName;
                            }

                            var confKey = row.Keys.FirstOrDefault(k => k.Equals("Conferência", StringComparison.OrdinalIgnoreCase) || k.Equals("conferencia", StringComparison.OrdinalIgnoreCase));
                            var statusKey = row.Keys.FirstOrDefault(k => k.Equals("Status", StringComparison.OrdinalIgnoreCase));
                            
                            if (confKey != null)
                            {
                                var statusValue = MapConferenciaToStatus(row[confKey]);
                                if (statusKey != null) row[statusKey] = statusValue;
                                else row["Status"] = statusValue; 
                            }

                            foreach (var col in filteredColumns)
                            {
                                var val = row[col];
                                if (col.Contains("Data", StringComparison.OrdinalIgnoreCase) || col.Contains("Dt", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (DateTime.TryParse(val, out var dt))
                                    {
                                        val = dt.ToString("MM/dd/yyyy");
                                    }
                                    else if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double oaDate))
                                    {
                                        try { val = DateTime.FromOADate(oaDate).ToString("MM/dd/yyyy"); } catch { }
                                    }
                                }
                                csv.WriteField(val);
                            }
                            
                            csv.WriteField(email ?? "");
                            csv.NextRecord();
                        }
                        skip += take;
                    }
                }
            }

            return memoryStream.ToArray();
        }

        private string MapConferenciaToStatus(string conferencia)
        {
            var normalized = conferencia.Trim().ToUpper();
            return normalized switch
            {
                "NORMAL" => "Active",
                "NCONT 1 AT" => "Late1",
                "NCONT 2 AT" => "Late2",
                "SUJ. A CANCELAMENTO" => "Late3",
                "EXCLUIDO" or "DESISTENTE" => "Defaulted",
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
    }
}
