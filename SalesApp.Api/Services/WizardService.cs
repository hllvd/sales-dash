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

            // Parse file
            var allRows = await _fileParser.ParseFileAsync(file);
            var columns = await _fileParser.GetColumnsAsync(file);

            // Inject virtual columns for dashboard files if needed (same logic as ImportsController)
            // This helps the preview and suggested mappings
            var virtualCols = new List<string> { "cota.group", "cota.cota", "cota.customer", "cota.contract" };
            foreach (var col in virtualCols)
            {
                if (!columns.Contains(col)) columns.Add(col);
            }

            foreach (var row in allRows)
            {
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
            }

            // Try to find the contractDashboard template to link the session
            var template = await _templateRepository.GetByNameAsync("contractDashboard");
            int? templateId = template?.Id;

            // Create import session and store file data
            var session = new ImportSession
            {
                UploadId = uploadId,
                TemplateId = templateId,
                FileName = file.FileName,
                FileType = fileType,
                UploadedByUserId = userId,
                Status = "wizard_step1",
                TotalRows = allRows.Count,
                FileData = JsonSerializer.Serialize(allRows)
            };

            await _sessionRepository.CreateAsync(session);

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
                
                // Overlay default mappings from template (high priority) - CRITICAL for dashboard aliased columns
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
                    // If less than 50% of required fields are matched, it's a likely mismatch
                    if (mappedRequiredFieldsCount < (requiredFields.Count + 1) / 2)
                    {
                        isTemplateMatch = false;
                        matchMessage = $"Atenção: O arquivo enviado não parece corresponder ao modelo '{template.Name}'. Foram identificados apenas {mappedRequiredFieldsCount} de {requiredFields.Count} campos obrigatórios.";
                    }
                    // Special case for contractDashboard: if it doesn't match the most critical fields
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
                SampleRows = allRows.Take(5).ToList(),
                TotalRows = allRows.Count,
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
            if (session == null || string.IsNullOrEmpty(session.FileData))
            {
                throw new ArgumentException("Session not found or empty");
            }

            var rows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(session.FileData) ?? new();
            
            // Extract unique Name and Matricula
            var userMap = new HashSet<(string Name, string Matricula)>();

            foreach (var row in rows)
            {
                var nameVal = GetColumnValue(row, "Comissionado", "Name", "name", "Nome", "Vendedor", "Usuário");
                var matVal = GetColumnValue(row, "Matrícula", "Matricula", "matricula", "Mat", "ID");

                if (!string.IsNullOrEmpty(nameVal) && !string.IsNullOrEmpty(matVal))
                {
                    userMap.Add((nameVal.Trim(), matVal.Trim()));
                }
            }

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                // Add UTF-8 BOM for Excel compatibility
                writer.Write('\uFEFF');
                
                using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    csv.WriteField("Name");
                    csv.WriteField("Email");
                    csv.WriteField("ParentEmail");
                    csv.WriteField("Matricula");
                    csv.WriteField("Owner_Matricula");
                    csv.NextRecord();

                    foreach (var user in userMap.OrderBy(u => u.Name))
                    {
                        csv.WriteField(user.Name);
                        csv.WriteField(""); // Email
                        csv.WriteField(""); // ParentEmail
                        csv.WriteField(user.Matricula);
                        csv.WriteField("1"); // Default to owner/proprietário
                        csv.NextRecord();
                    }
                }
            }

            return memoryStream.ToArray();
        }

        public async Task<ImportStatusResponse> ProcessStep2ImportAsync(string uploadId, IFormFile usersFile, Guid userId)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null || string.IsNullOrEmpty(session.FileData))
            {
                throw new ArgumentException("Original session not found");
            }

            // 1. Parse users file
            var userRows = await _fileParser.ParseFileAsync(usersFile);
            
            // 2. Import Users (and matriculas via user import)
            var userMappings = new Dictionary<string, string>
            {
                ["Name"] = "Name",
                ["Email"] = "Email",
                ["ParentEmail"] = "ParentEmail",
                ["Matricula"] = "Matricula",
                ["Owner_Matricula"] = "IsMatriculaOwner"
            };

            // Execute user import
            var userResult = await _importExecution.ExecuteUserImportAsync(
                uploadId,
                session.Id,
                userRows,
                userMappings
            );

            // 3. Update session status for History tracking
            // Use standard statuses so it appears in "Histórico de Importação"
            session.Status = userResult.FailedRows > 0 ? "completed_with_errors" : "completed";
            session.CompletedAt = DateTime.UtcNow;
            session.ProcessedRows = userResult.ProcessedRows;
            session.FailedRows = userResult.FailedRows;
            
            // Resolve Users template by name to avoid FK errors
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
            if (session == null || string.IsNullOrEmpty(session.FileData))
            {
                throw new ArgumentException("Session not found or empty");
            }

            var originalRows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(session.FileData) ?? new();
            if (originalRows.Count == 0) return Array.Empty<byte>();

            // Get original columns in order
            var columns = originalRows.First().Keys.ToList();
            
            // Build database lookups for fancy resolution
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
                writer.Write('\uFEFF'); // BOM
                
                using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    // Filter columns: remove Q (16) and R (17) and virtual helper columns
                    var filteredColumns = columns
                        .Where((c, index) => index != 16 && index != 17)
                        .Where(c => !c.StartsWith("cota.", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    // Add Email column
                    foreach (var col in filteredColumns) csv.WriteField(col);
                    csv.WriteField("Email");
                    csv.NextRecord();

                    foreach (var row in originalRows)
                    {
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

                        // Resolve and update Status if "Conferência" exists
                        var confKey = row.Keys.FirstOrDefault(k => k.Equals("Conferência", StringComparison.OrdinalIgnoreCase) || k.Equals("conferencia", StringComparison.OrdinalIgnoreCase));
                        var statusKey = row.Keys.FirstOrDefault(k => k.Equals("Status", StringComparison.OrdinalIgnoreCase));
                        
                        if (confKey != null)
                        {
                            var statusValue = MapConferenciaToStatus(row[confKey]);
                            if (statusKey != null) row[statusKey] = statusValue;
                            else row["Status"] = statusValue; // Add if not exists
                        }

                        foreach (var col in filteredColumns)
                        {
                            var val = row[col];
                            
                            // Try to format dates
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
