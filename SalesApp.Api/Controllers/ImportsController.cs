using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;
using SalesApp.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImportsController : ControllerBase
    {
        private readonly IImportTemplateRepository _templateRepository;
        private readonly IImportSessionRepository _sessionRepository;
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IFileParserService _fileParser;
        private readonly IAutoMappingService _autoMapping;
        private readonly IImportValidationService _validation;
        private readonly IImportExecutionService _importExecution;
        private readonly AppDbContext _context;

        public ImportsController(
            IImportTemplateRepository templateRepository,
            IImportSessionRepository sessionRepository,
            IContractRepository contractRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IFileParserService fileParser,
            IAutoMappingService autoMapping,
            IImportValidationService validation,
            IImportExecutionService importExecution,
            AppDbContext context)
        {
            _templateRepository = templateRepository;
            _sessionRepository = sessionRepository;
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _fileParser = fileParser;
            _autoMapping = autoMapping;
            _validation = validation;
            _importExecution = importExecution;
            _context = context;
        }

        #region Template Management
        
        private static readonly List<ImportTemplate> HardcodedTemplates = new()
        {
            new ImportTemplate
            {
                Id = 1,
                Name = "Users",
                EntityType = "User",
                Description = "Template for importing users",
                RequiredFields = JsonSerializer.Serialize(new List<string> { "Name", "Email" }),
                OptionalFields = JsonSerializer.Serialize(new List<string> { "Surname", "Role", "ParentEmail", "SendEmail" }),
                DefaultMappings = "{}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ImportTemplate
            {
                Id = 2,
                Name = "Contracts",
                EntityType = "Contract",
                Description = "Template for importing contracts",
                RequiredFields = JsonSerializer.Serialize(new List<string> { "ContractNumber", "UserEmail", "TotalAmount" }),
                OptionalFields = JsonSerializer.Serialize(new List<string> { "GroupId", "Status", "SaleStartDate", "SaleEndDate", "ContractType", "Quota", "PvId", "CustomerName", "Version" }),
                DefaultMappings = "{}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ImportTemplate
            {
                Id = 3,
                Name = "contractDashboard",
                EntityType = "Contract",
                Description = "Template for contract dashboard import from Power BI",
                RequiredFields = JsonSerializer.Serialize(new List<string> { 
                    "ContractNumber", "TotalAmount", "SaleStartDate", "GroupId", "Quota", "CustomerName" 
                }),
                OptionalFields = JsonSerializer.Serialize(new List<string> { 
                    "Status", "PvId", "PvName", "Version", "Matricula", "Category", "PlanoVenda" 
                }),
                DefaultMappings = JsonSerializer.Serialize(new Dictionary<string, string> {
                    { "cota.group", "GroupId" },
                    { "cota.cota", "Quota" },
                    { "cota.customer", "CustomerName" },
                    { "cota.contract", "ContractNumber" },
                    { "DtVenda", "SaleStartDate" },
                    { "Dt Venda", "SaleStartDate" },
                    { "Situação Cobrança", "Status" },
                    { "SituacaoCobranca", "Status" },
                    { "CodPV", "PvId" },
                    { "Cód. PV", "PvId" },
                    { "PV", "PvName" },
                    { "Versao", "Version" },
                    { "Matricula", "TempMatricula" },
                    { "Categoria", "Category" },
                    { "PlanoVenda", "PlanoVenda" }
                }),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        [HttpGet("templates")]
        [HasPermission("imports:execute")]
        public ActionResult<ApiResponse<List<ImportTemplateResponse>>> GetTemplates([FromQuery] string? entityType = null)
        {
            var isSuperAdmin = User.HasClaim("perm", "system:superadmin");
            
            var templates = HardcodedTemplates
                .Where(t => isSuperAdmin || t.Name == "contractDashboard")
                .Where(t => string.IsNullOrEmpty(entityType) || t.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var response = templates.Select(MapToTemplateResponse).ToList();

            return Ok(new ApiResponse<List<ImportTemplateResponse>>
            {
                Success = true,
                Data = response,
                Message = "Templates retrieved successfully"
            });
        }

        [HttpGet("templates/{id}")]
        [HasPermission("imports:execute")]
        public ActionResult<ApiResponse<ImportTemplateResponse>> GetTemplate(int id)
        {
            var template = HardcodedTemplates.FirstOrDefault(t => t.Id == id);
            if (template == null)
            {
                return NotFound(new ApiResponse<ImportTemplateResponse>
                {
                    Success = false,
                    Message = "Template not found"
                });
            }

            return Ok(new ApiResponse<ImportTemplateResponse>
            {
                Success = true,
                Data = MapToTemplateResponse(template),
                Message = "Template retrieved successfully"
            });
        }

        #endregion

        #region Import Workflow

        [HttpPost("upload")]
        [HasPermission("imports:execute")]
        public async Task<ActionResult<ApiResponse<ImportPreviewResponse>>> UploadFile(IFormFile file, int templateId)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<ImportPreviewResponse>
                {
                    Success = false,
                    Message = "No file uploaded"
                });
            }

            try
            {
                var fileType = _fileParser.GetFileType(file);
                var uploadId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";

                var hardcodedTemplate = HardcodedTemplates.FirstOrDefault(t => t.Id == templateId);
                if (hardcodedTemplate == null)
                {
                    return BadRequest(new ApiResponse<ImportPreviewResponse>
                    {
                        Success = false,
                        Message = "Template not found"
                    });
                }

                if (!User.HasClaim("perm", "system:superadmin") && hardcodedTemplate.Name != "contractDashboard")
                {
                    return StatusCode(403, new ApiResponse<ImportPreviewResponse>
                    {
                        Success = false,
                        Message = "You do not have permission to use this template"
                    });
                }

                var dbTemplate = await _templateRepository.GetByNameAsync(hardcodedTemplate.Name);
                if (dbTemplate == null)
                {
                    var currentUserId = GetCurrentUserId();
                    var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                    if (currentUser == null)
                    {
                        var anyAdmin = await _userRepository.GetRootUserAsync();
                        currentUserId = anyAdmin?.Id ?? currentUserId;
                    }

                    dbTemplate = new ImportTemplate
                    {
                        Name = hardcodedTemplate.Name,
                        EntityType = hardcodedTemplate.EntityType,
                        Description = hardcodedTemplate.Description,
                        RequiredFields = hardcodedTemplate.RequiredFields,
                        OptionalFields = hardcodedTemplate.OptionalFields,
                        DefaultMappings = hardcodedTemplate.DefaultMappings,
                        CreatedByUserId = currentUserId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _templateRepository.CreateAsync(dbTemplate);
                }
                else
                {
                    dbTemplate.RequiredFields = hardcodedTemplate.RequiredFields;
                    dbTemplate.OptionalFields = hardcodedTemplate.OptionalFields;
                    dbTemplate.Description = hardcodedTemplate.Description;
                    dbTemplate.UpdatedAt = DateTime.UtcNow;
                    await _templateRepository.UpdateAsync(dbTemplate);
                }

                var session = new ImportSession
                {
                    UploadId = uploadId,
                    TemplateId = dbTemplate.Id,
                    FileName = file.FileName,
                    FileType = fileType,
                    UploadedByUserId = GetCurrentUserId(),
                    Status = "preview",
                    TotalRows = 0
                };

                await _sessionRepository.CreateAsync(session);

                // ✅ Parse the entire file into a bounded list first.
                // Streaming iterators (IAsyncEnumerable) can loop indefinitely when the underlying
                // file format reports a row-count that is far larger than actual data (e.g. XLSX
                // worksheet.Dimension.Rows, or CsvHelper on a malformed file). By collecting all
                // rows upfront we get a guaranteed termination point and can apply a hard cap.
                const int maxAllowedRows = 100_000;
                var parsedRows = await _fileParser.ParseFileAsync(file);

                if (parsedRows.Count > maxAllowedRows)
                {
                    return BadRequest(new ApiResponse<ImportPreviewResponse>
                    {
                        Success = false,
                        Message = $"O arquivo contém mais de {maxAllowedRows:N0} linhas. Divida o arquivo em partes menores."
                    });
                }

                var virtualCols = new List<string> { "cota.group", "cota.cota", "cota.customer", "cota.contract" };

                // Enrich contractDashboard rows and extract preview
                var allRowsForPreview = new List<Dictionary<string, string>>();
                var importRows = new List<ImportRow>();

                for (int rowIndex = 0; rowIndex < parsedRows.Count; rowIndex++)
                {
                    var row = parsedRows[rowIndex];

                    if (hardcodedTemplate.Name == "contractDashboard")
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

                    if (rowIndex < 10) allRowsForPreview.Add(new Dictionary<string, string>(row));

                    importRows.Add(new ImportRow
                    {
                        ImportSessionId = session.Id,
                        RowIndex = rowIndex,
                        RowData = JsonSerializer.Serialize(row)
                    });
                }

                // Batch-insert all ImportRows
                for (int i = 0; i < importRows.Count; i += 500)
                {
                    var batch = importRows.Skip(i).Take(500).ToList();
                    await _context.ImportRows.AddRangeAsync(batch);
                    await _context.SaveChangesAsync();
                }

                // Extract columns from the first parsed row
                var columns = parsedRows.Count > 0 ? parsedRows[0].Keys.ToList() : new List<string>();
                if (hardcodedTemplate.Name == "contractDashboard")
                {
                    foreach (var col in virtualCols)
                    {
                        if (!columns.Contains(col)) columns.Add(col);
                    }
                }

                session.TotalRows = parsedRows.Count;
                await _sessionRepository.UpdateAsync(session);

                var entityType = hardcodedTemplate.EntityType;
                var requiredFields = JsonSerializer.Deserialize<List<string>>(hardcodedTemplate.RequiredFields) ?? new();
                var optionalFields = JsonSerializer.Deserialize<List<string>>(hardcodedTemplate.OptionalFields) ?? new();
                var allTemplateFields = new List<string>();
                allTemplateFields.AddRange(requiredFields);
                allTemplateFields.AddRange(optionalFields);
                
                var suggestedMappings = _autoMapping.SuggestMappings(columns, entityType, allTemplateFields);
                
                if (!string.IsNullOrEmpty(hardcodedTemplate.DefaultMappings) && hardcodedTemplate.DefaultMappings != "{}")
                {
                    var templateMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(hardcodedTemplate.DefaultMappings) ?? new();
                    var appliedMappings = _autoMapping.ApplyTemplateMappings(templateMappings, columns);
                    foreach (var (src, target) in appliedMappings) suggestedMappings[src] = target;
                }

                var mappedRequiredFieldsCount = requiredFields.Count(rf => suggestedMappings.Values.Contains(rf));
                var isTemplateMatch = true;
                string? matchMessage = null;

                if (requiredFields.Any())
                {
                    if (mappedRequiredFieldsCount < (requiredFields.Count + 1) / 2)
                    {
                        isTemplateMatch = false;
                        matchMessage = $"Atenção: O arquivo enviado não parece corresponder ao modelo '{hardcodedTemplate.Name}'. Foram identificados apenas {mappedRequiredFieldsCount} de {requiredFields.Count} campos obrigatórios.";
                    }
                    else if (hardcodedTemplate.Name == "contractDashboard" && mappedRequiredFieldsCount < 3)
                    {
                        isTemplateMatch = false;
                        matchMessage = "Atenção: Este arquivo não parece ser um dashboard de contratos válido.";
                    }
                }

                return Ok(new ApiResponse<ImportPreviewResponse>
                {
                    Success = true,
                    Data = new ImportPreviewResponse
                    {
                        UploadId = uploadId,
                        SessionId = uploadId,
                        TemplateId = templateId,
                        TemplateName = hardcodedTemplate.Name,
                        EntityType = entityType,
                        FileName = file.FileName,
                        DetectedColumns = columns,
                        SampleRows = allRowsForPreview.Take(5).ToList(),
                        TotalRows = parsedRows.Count,
                        SuggestedMappings = suggestedMappings,
                        RequiredFields = requiredFields,
                        OptionalFields = optionalFields,
                        IsTemplateMatch = isTemplateMatch,
                        MatchMessage = matchMessage
                    },
                    Message = "File uploaded and preview generated successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ImportPreviewResponse>
                {
                    Success = false,
                    Message = $"Error processing file: {ex.Message}"
                });
            }
        }

        [HttpPost("{uploadId}/mappings")]
        [HasPermission("imports:execute")]
        public async Task<ActionResult<ApiResponse<ImportStatusResponse>>> ConfigureMappings(string uploadId, ColumnMappingRequest request)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                return NotFound(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = "Import session not found"
                });
            }

            try
            {
                var allRows = new List<Dictionary<string, string>>();
                int skip = 0;
                while (true)
                {
                    var chunk = await _context.ImportRows
                        .Where(r => r.ImportSessionId == session.Id)
                        .OrderBy(r => r.RowIndex)
                        .Skip(skip)
                        .Take(500)
                        .ToListAsync();
                    if (chunk.Count == 0) break;
                    allRows.AddRange(chunk.Select(c => JsonSerializer.Deserialize<Dictionary<string, string>>(c.RowData) ?? new()));
                    skip += 500;
                    if (allRows.Count > 5000) break;
                }

                var entityType = session.Template?.EntityType ?? "Contract";
                var requiredFields = session.Template?.RequiredFields != null ? JsonSerializer.Deserialize<List<string>>(session.Template.RequiredFields) : new List<string>();
                var allowAutoCreateGroups = request?.AllowAutoCreateGroups ?? false;
                var allowAutoCreatePVs = request?.AllowAutoCreatePVs ?? false;
                var skipMissingContractNumber = request?.SkipMissingContractNumber ?? false;

                var validationErrors = await _validation.ValidateAllRowsAsync(allRows, request.Mappings, entityType, requiredFields, allowAutoCreateGroups, allowAutoCreatePVs, skipMissingContractNumber);

                session.Mappings = JsonSerializer.Serialize(request.Mappings);
                session.Status = "ready";
                await _sessionRepository.UpdateAsync(session);

                return Ok(new ApiResponse<ImportStatusResponse>
                {
                    Success = true,
                    Data = new ImportStatusResponse
                    {
                        UploadId = uploadId,
                        Status = session.Status,
                        TotalRows = session.TotalRows,
                        ProcessedRows = 0,
                        FailedRows = validationErrors.Count,
                        UnresolvedUsers = new List<UnresolvedUserInfo>(),
                        Errors = validationErrors.SelectMany(kvp => kvp.Value.Select(err => $"Row {kvp.Key + 1}: {err}")).ToList()
                    },
                    Message = "Mappings configured successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = $"Error configuring mappings: {ex.Message}"
                });
            }
        }

        [HttpPost("{uploadId}/confirm")]
        [HasPermission("imports:execute")]
        public async Task<ActionResult<ApiResponse<ImportStatusResponse>>> ConfirmImport(
            string uploadId,
            [FromBody] ConfirmImportRequest? request)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                return NotFound(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = "Import session not found"
                });
            }

            if (session.Status != "ready")
            {
                return BadRequest(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = "Import session is not ready."
                });
            }

            try
            {
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(session.Mappings ?? "{}") ?? new();
                if (mappings.Count == 0)
                {
                    return BadRequest(new ApiResponse<ImportStatusResponse>
                    {
                        Success = false,
                        Message = "No mappings configured."
                    });
                }

                ImportResult totalResult = new();
                var entityType = session.Template?.EntityType ?? "Contract";
                var templateName = session.Template?.Name ?? "";
                var dateFormat = request?.DateFormat ?? "MM/DD/YYYY";
                var skipMissingContractNumber = request?.SkipMissingContractNumber ?? false;
                var allowAutoCreateGroups = request?.AllowAutoCreateGroups ?? false;
                var allowAutoCreatePVs = request?.AllowAutoCreatePVs ?? false;

                int skipRows = 0;
                // ✅ Safety guard: cap iterations to prevent any possibility of an infinite loop.
                // Even with 0 rows, we loop at most once (chunk.Count == 0 triggers break).
                int maxChunks = (session.TotalRows / 500) + 2;
                int chunkIteration = 0;
                while (true)
                {
                    if (chunkIteration++ > maxChunks) break;

                    var chunk = await _context.ImportRows
                        .Where(r => r.ImportSessionId == session.Id)
                        .OrderBy(r => r.RowIndex)
                        .Skip(skipRows)
                        .Take(500)
                        .ToListAsync();

                    if (chunk.Count == 0) break;

                    var rows = chunk.Select(c => JsonSerializer.Deserialize<Dictionary<string, string>>(c.RowData) ?? new()).ToList();
                    
                    ImportResult result;
                    if (entityType == "User")
                    {
                        result = await _importExecution.ExecuteUserImportAsync(uploadId, session.Id, rows, mappings);
                    }
                    else if (templateName == "contractDashboard")
                    {
                        result = await _importExecution.ExecuteContractDashboardImportAsync(uploadId, session.Id, rows, mappings, skipMissingContractNumber, allowAutoCreateGroups, allowAutoCreatePVs);
                    }
                    else
                    {
                        result = await _importExecution.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, dateFormat, skipMissingContractNumber, allowAutoCreateGroups, allowAutoCreatePVs);
                    }

                    totalResult.ProcessedRows += result.ProcessedRows;
                    totalResult.FailedRows += result.FailedRows;
                    totalResult.Errors.AddRange(result.Errors);
                    totalResult.CreatedGroups.AddRange(result.CreatedGroups);
                    totalResult.CreatedPVs.AddRange(result.CreatedPVs);

                    skipRows += 500;
                }

                session.Status = totalResult.FailedRows > 0 ? "completed_with_errors" : "completed";
                session.CompletedAt = DateTime.UtcNow;
                session.ProcessedRows = totalResult.ProcessedRows;
                session.FailedRows = totalResult.FailedRows;
                await _sessionRepository.UpdateAsync(session);

                var successMessage = totalResult.FailedRows > 0 
                    ? $"Import completed with {totalResult.FailedRows} errors. {totalResult.ProcessedRows} items created."
                    : $"Import completed successfully. {totalResult.ProcessedRows} items created.";

                return Ok(new ApiResponse<ImportStatusResponse>
                {
                    Success = true,
                    Data = new ImportStatusResponse
                    {
                        UploadId = uploadId,
                        Status = session.Status,
                        TotalRows = session.TotalRows,
                        ProcessedRows = totalResult.ProcessedRows,
                        FailedRows = totalResult.FailedRows,
                        UnresolvedUsers = new List<UnresolvedUserInfo>(),
                        CreatedGroups = totalResult.CreatedGroups.Distinct().ToList(),
                        CreatedPVs = totalResult.CreatedPVs.Distinct().ToList(),
                        Errors = totalResult.Errors
                    },
                    Message = successMessage
                });
            }
            catch (Exception ex)
            {
                session.Status = "failed";
                await _sessionRepository.UpdateAsync(session);
                return BadRequest(new ApiResponse<ImportStatusResponse> { Success = false, Message = $"Error executing import: {ex.Message}" });
            }
        }

        [HttpGet("{uploadId}/status")]
        [HasPermission("imports:execute")]
        public async Task<ActionResult<ApiResponse<ImportStatusResponse>>> GetStatus(string uploadId)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId);
            if (session == null)
            {
                return NotFound(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = "Import session not found"
                });
            }

            var response = new ImportStatusResponse
            {
                UploadId = uploadId,
                Status = session.Status,
                TotalRows = session.TotalRows,
                ProcessedRows = session.ProcessedRows,
                FailedRows = session.FailedRows,
                UnresolvedUsers = new List<UnresolvedUserInfo>()
            };

            return Ok(new ApiResponse<ImportStatusResponse>
            {
                Success = true,
                Data = response,
                Message = "Status retrieved successfully"
            });
        }

        [HttpDelete("{uploadId}")]
        [HasPermission("imports:rollback")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteByUploadId(string uploadId)
        {
            var contracts = await _contractRepository.GetByUploadIdAsync(uploadId);
            
            if (!contracts.Any())
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "No contracts found for this upload ID"
                });
            }

            foreach (var contract in contracts)
            {
                contract.IsActive = false;
                await _contractRepository.UpdateAsync(contract);
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Successfully deleted {contracts.Count} contracts"
            });
        }

        [HttpGet("sessions")]
        [HasPermission("imports:history")]
        public async Task<ActionResult<ApiResponse<List<object>>>> GetSessions()
        {
            var sessions = await _sessionRepository.GetAllAsync();

            var response = sessions.Select(s => new
            {
                s.Id,
                s.UploadId,
                s.FileName,
                s.FileType,
                TemplateName = s.Template?.Name,
                UploadedBy = s.UploadedBy?.Name,
                s.Status,
                s.TotalRows,
                s.ProcessedRows,
                s.FailedRows,
                s.CreatedAt,
                s.CompletedAt
            }).ToList<object>();

            return Ok(new ApiResponse<List<object>>
            {
                Success = true,
                Data = response,
                Message = "Sessions retrieved successfully"
            });
        }

        #endregion

        #region Helper Methods

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private ImportTemplateResponse MapToTemplateResponse(ImportTemplate template)
        {
            return new ImportTemplateResponse
            {
                Id = template.Id,
                Name = template.Name,
                EntityType = template.EntityType,
                Description = template.Description,
                RequiredFields = JsonSerializer.Deserialize<List<string>>(template.RequiredFields) ?? new(),
                OptionalFields = JsonSerializer.Deserialize<List<string>>(template.OptionalFields) ?? new(),
                DefaultMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(template.DefaultMappings) ?? new(),
                IsActive = template.IsActive,
                CreatedAt = template.CreatedAt
            };
        }

        #endregion
    }
}
