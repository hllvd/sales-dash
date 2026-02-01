using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using System.Security.Claims;
using System.Text.Json;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImportsController : ControllerBase
    {
        private readonly IImportTemplateRepository _templateRepository;
        private readonly IImportSessionRepository _sessionRepository;
        private readonly IImportUserMappingRepository _userMappingRepository;
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IFileParserService _fileParser;
        private readonly IAutoMappingService _autoMapping;
        private readonly IUserMatchingService _userMatching;
        private readonly IImportValidationService _validation;
        private readonly IImportExecutionService _importExecution;

        public ImportsController(
            IImportTemplateRepository templateRepository,
            IImportSessionRepository sessionRepository,
            IImportUserMappingRepository userMappingRepository,
            IContractRepository contractRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IFileParserService fileParser,
            IAutoMappingService autoMapping,
            IUserMatchingService userMatching,
            IImportValidationService validation,
            IImportExecutionService importExecution)
        {
            _templateRepository = templateRepository;
            _sessionRepository = sessionRepository;
            _userMappingRepository = userMappingRepository;
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _fileParser = fileParser;
            _autoMapping = autoMapping;
            _userMatching = userMatching;
            _validation = validation;
            _importExecution = importExecution;
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
                OptionalFields = JsonSerializer.Serialize(new List<string> { "GroupId", "Status", "SaleStartDate", "SaleEndDate", "ContractType", "Quota", "PvId", "CustomerName" }),
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
                    "Status", "PvId", "Version", "TempMatricula", "Category", "PlanoVenda" 
                }),
                DefaultMappings = JsonSerializer.Serialize(new Dictionary<string, string> {
                    { "cota.group", "GroupId" },
                    { "cota.cota", "Quota" },
                    { "cota.customer", "CustomerName" },
                    { "cota.contract", "ContractNumber" },
                    { "ProducaoAnalitica", "TotalAmount" },
                    { "Produção Analitica", "TotalAmount" },
                    { "Producao Analitica", "TotalAmount" },
                    { "DtVenda", "SaleStartDate" },
                    { "Dt Venda", "SaleStartDate" },
                    { "SituacaoCobranca", "Status" },
                    { "CodPV", "PvId" },
                    { "Cód. PV", "PvId" },
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
        [Authorize(Roles = "admin,superadmin")]
        public ActionResult<ApiResponse<List<ImportTemplateResponse>>> GetTemplates([FromQuery] string? entityType = null)
        {
            // Can be optimized the speed?
            var isSuperAdmin = User.IsInRole("superadmin");
            
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
        [Authorize(Roles = "admin,superadmin")]
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
        [Authorize(Roles = "admin,superadmin")]
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

                // Parse file
                var allRows = await _fileParser.ParseFileAsync(file);
                var columns = await _fileParser.GetColumnsAsync(file);

                // Get template (Hardcoded)
                var hardcodedTemplate = HardcodedTemplates.FirstOrDefault(t => t.Id == templateId);
                if (hardcodedTemplate == null)
                {
                    return BadRequest(new ApiResponse<ImportPreviewResponse>
                    {
                        Success = false,
                        Message = "Template not found"
                    });
                }

                // If template is contractDashboard, split Cota into virtual columns for custom mapping
                if (hardcodedTemplate.Name == "contractDashboard")
                {
                    // Add virtual column headers
                    var virtualCols = new List<string> { "cota.group", "cota.cota", "cota.customer", "cota.contract" };
                    foreach (var col in virtualCols)
                    {
                        if (!columns.Contains(col)) columns.Add(col);
                    }

                    // Process rows to extract values
                    foreach (var row in allRows)
                    {
                        // Initialize virtual columns to avoid KeyNotFoundException during validation
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
                }

                // Security Check: Admins can only use contractDashboard
                if (!User.IsInRole("superadmin") && hardcodedTemplate.Name != "contractDashboard")
                {
                    return StatusCode(403, new ApiResponse<ImportPreviewResponse>
                    {
                        Success = false,
                        Message = "You do not have permission to use this template"
                    });
                }

                // Sync with DB to ensure FK validity and keep fields updated
                var dbTemplate = await _templateRepository.GetByNameAsync(hardcodedTemplate.Name);
                if (dbTemplate == null)
                {
                    var currentUserId = GetCurrentUserId();
                    var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                    
                    // Fallback to first available admin if current user is not found (stale session/reseeded DB)
                    if (currentUser == null)
                    {
                        Console.WriteLine($"[ImportsController] Current user {currentUserId} not found. Falling back to first admin.");
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
                    // Update existing template to match hardcoded values
                    dbTemplate.RequiredFields = hardcodedTemplate.RequiredFields;
                    dbTemplate.OptionalFields = hardcodedTemplate.OptionalFields;
                    dbTemplate.Description = hardcodedTemplate.Description;
                    dbTemplate.UpdatedAt = DateTime.UtcNow;
                    await _templateRepository.UpdateAsync(dbTemplate);
                }

                var entityType = hardcodedTemplate.EntityType;
                
                // Get required and optional fields
                var requiredFields = JsonSerializer.Deserialize<List<string>>(hardcodedTemplate.RequiredFields) ?? new();
                var optionalFields = JsonSerializer.Deserialize<List<string>>(hardcodedTemplate.OptionalFields) ?? new();
                
                // Combine all template fields for auto-mapping
                var allTemplateFields = new List<string>();
                allTemplateFields.AddRange(requiredFields);
                allTemplateFields.AddRange(optionalFields);
                
                // Use SuggestMappings for pattern/exact field matching
                var suggestedMappings = _autoMapping.SuggestMappings(columns, entityType, allTemplateFields);
                
                // Overlay default mappings from template (high priority)
                if (!string.IsNullOrEmpty(hardcodedTemplate.DefaultMappings) && hardcodedTemplate.DefaultMappings != "{}")
                {
                    var templateMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(hardcodedTemplate.DefaultMappings) ?? new();
                    var appliedMappings = _autoMapping.ApplyTemplateMappings(templateMappings, columns);
                    
                    foreach (var (src, target) in appliedMappings)
                    {
                        suggestedMappings[src] = target;
                    }
                }

                // Create import session and store file data
                var session = new ImportSession
                {
                    UploadId = uploadId,
                    TemplateId = dbTemplate.Id, // Use DB ID
                    FileName = file.FileName,
                    FileType = fileType,
                    UploadedByUserId = GetCurrentUserId(),
                    Status = "preview",
                    TotalRows = allRows.Count,
                    FileData = JsonSerializer.Serialize(allRows) // Store file data for later
                };

                await _sessionRepository.CreateAsync(session);

                var response = new ImportPreviewResponse
                {
                    UploadId = uploadId,
                    SessionId = uploadId,
                    TemplateId = templateId,
                    TemplateName = hardcodedTemplate.Name,
                    EntityType = entityType,
                    FileName = file.FileName,
                    DetectedColumns = columns,
                    SampleRows = allRows.Take(5).ToList(),
                    TotalRows = allRows.Count,
                    SuggestedMappings = suggestedMappings,
                    RequiredFields = requiredFields,
                    OptionalFields = optionalFields
                };

                return Ok(new ApiResponse<ImportPreviewResponse>
                {
                    Success = true,
                    Data = response,
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
        [Authorize(Roles = "admin,superadmin")]
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
                // Get file data from session
                var allRows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(session.FileData ?? "[]") ?? new();

                var entityType = session.Template?.EntityType ?? "Contract";

                // Validate all rows
                var validationErrors = await _validation.ValidateAllRowsAsync(allRows, request.Mappings, entityType);

                // Store mappings and update session status
                session.Mappings = JsonSerializer.Serialize(request.Mappings);
                session.Status = "ready"; // No user resolution needed for contracts anymore
                await _sessionRepository.UpdateAsync(session);

                var response = new ImportStatusResponse
                {
                    UploadId = uploadId,
                    Status = session.Status,
                    TotalRows = session.TotalRows,
                    ProcessedRows = 0,
                    FailedRows = validationErrors.Count,
                    UnresolvedUsers = new List<UnresolvedUserInfo>(),
                    Errors = validationErrors.SelectMany(kvp => kvp.Value.Select(err => $"Row {kvp.Key + 1}: {err}")).ToList()
                };

                return Ok(new ApiResponse<ImportStatusResponse>
                {
                    Success = true,
                    Data = response,
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

        [HttpPost("{uploadId}/users")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ImportStatusResponse>>> ResolveUsers(string uploadId, UserMappingRequest request)
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
                var currentUserId = GetCurrentUserId();

                foreach (var mapping in request.UserMappings)
                {
                    Guid? resolvedUserId = null;
                    string action = "pending";

                    if (mapping.Action == "map" && mapping.TargetUserId.HasValue)
                    {
                        resolvedUserId = mapping.TargetUserId.Value;
                        action = "mapped";
                    }
                    else if (mapping.Action == "create" && !string.IsNullOrEmpty(mapping.NewUserEmail))
                    {
                        var newUser = await _userMatching.CreateUserFromImportAsync(
                            mapping.SourceName,
                            mapping.SourceSurname,
                            mapping.NewUserEmail,
                            currentUserId);

                        resolvedUserId = newUser.Id;
                        action = "created";
                    }

                    var userMapping = new ImportUserMapping
                    {
                        ImportSessionId = session.Id,
                        SourceName = mapping.SourceName,
                        SourceSurname = mapping.SourceSurname,
                        ResolvedUserId = resolvedUserId,
                        Action = action
                    };

                    await _userMappingRepository.CreateAsync(userMapping);
                }

                // Update session status
                session.Status = "ready";
                await _sessionRepository.UpdateAsync(session);

                var response = new ImportStatusResponse
                {
                    UploadId = uploadId,
                    Status = session.Status,
                    TotalRows = session.TotalRows,
                    ProcessedRows = 0,
                    FailedRows = 0,
                    UnresolvedUsers = new List<UnresolvedUserInfo>()
                };

                return Ok(new ApiResponse<ImportStatusResponse>
                {
                    Success = true,
                    Data = response,
                    Message = "User mappings resolved successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = $"Error resolving users: {ex.Message}"
                });
            }
        }

        [HttpPost("{uploadId}/confirm")]
        [Authorize(Roles = "admin,superadmin")]
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
                    Message = "Import session is not ready. Please configure mappings and resolve users first."
                });
            }

            try
            {
                // Get stored file data and mappings
                var allRows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(session.FileData ?? "[]") ?? new();
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(session.Mappings ?? "{}") ?? new();

                if (allRows.Count == 0)
                {
                    return BadRequest(new ApiResponse<ImportStatusResponse>
                    {
                        Success = false,
                        Message = "No file data found. Please upload file again."
                    });
                }

                if (mappings.Count == 0)
                {
                    return BadRequest(new ApiResponse<ImportStatusResponse>
                    {
                        Success = false,
                        Message = "No mappings configured. Please configure mappings first."
                    });
                }

                // Execute import
                ImportResult result;
                var entityType = session.Template?.EntityType ?? "Contract";
                var templateName = session.Template?.Name ?? "";
                var dateFormat = request?.DateFormat ?? "MM/DD/YYYY";

                if (entityType == "User")
                {
                    result = await _importExecution.ExecuteUserImportAsync(
                        uploadId,
                        allRows,
                        mappings);
                }
                else if (templateName == "contractDashboard")
                {
                    // Use specialized contractDashboard import
                    result = await _importExecution.ExecuteContractDashboardImportAsync(
                        uploadId,
                        allRows,
                        mappings);
                }
                else
                {
                    // Contract imports now use UserEmail directly, no user mapping needed
                    result = await _importExecution.ExecuteContractImportAsync(
                        uploadId,
                        allRows,
                        mappings,
                        dateFormat);
                }

                // Update session with results
                session.Status = result.FailedRows > 0 ? "completed_with_errors" : "completed";
                session.CompletedAt = DateTime.UtcNow;
                session.ProcessedRows = result.ProcessedRows;
                session.FailedRows = result.FailedRows;
                await _sessionRepository.UpdateAsync(session);

                var response = new ImportStatusResponse
                {
                    UploadId = uploadId,
                    Status = session.Status,
                    TotalRows = session.TotalRows,
                    ProcessedRows = session.ProcessedRows,
                    FailedRows = session.FailedRows,
                    UnresolvedUsers = new List<UnresolvedUserInfo>(),
                    CreatedGroups = result.CreatedGroups,
                    Errors = result.Errors
                };

                var successMessage = result.FailedRows > 0 
                    ? $"Import completed with {result.FailedRows} errors. {result.ProcessedRows} contracts created successfully."
                    : $"Import completed successfully. {result.ProcessedRows} contracts created.";

                if (result.CreatedGroups.Any())
                {
                    successMessage += $" {result.CreatedGroups.Count} new groups were automatically created: {string.Join(", ", result.CreatedGroups)}";
                }

                return Ok(new ApiResponse<ImportStatusResponse>
                {
                    Success = true,
                    Data = response,
                    Message = successMessage
                });
            }
            catch (Exception ex)
            {
                session.Status = "failed";
                await _sessionRepository.UpdateAsync(session);

                return BadRequest(new ApiResponse<ImportStatusResponse>
                {
                    Success = false,
                    Message = $"Error executing import: {ex.Message}"
                });
            }
        }

        [HttpGet("{uploadId}/status")]
        [Authorize(Roles = "admin,superadmin")]
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
        [Authorize(Roles = "superadmin")]
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
        [Authorize(Roles = "admin,superadmin")]
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
