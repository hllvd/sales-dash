using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Attributes;
using SalesApp.Utils;
using System.Security.Claims;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContractsController : ControllerBase
    {
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IContractAggregationService _aggregationService;
        private readonly IUserMatriculaRepository _userMatriculaRepository;
        private readonly IMatriculaRepository _matriculaRepository;
        private readonly IMessageService _messageService;
        private readonly IUserScopeService _userScopeService;
        private readonly IExportService _exportService;
        private readonly IContractStatusMapper _statusMapper;
        private readonly IContractStatusService _statusService;
        private readonly IPendingContractClaimRepository _pendingClaimRepository;

        [HttpGet("/api/monitoring/contracts")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<MatriculaHealthResponse>>>> GetMatriculaHealth()
        {
            var healthData = await _contractRepository.GetMatriculaHealthAsync();
            return Ok(new ApiResponse<List<MatriculaHealthResponse>>
            {
                Success = true,
                Data = healthData,
                Message = "Matricula health data retrieved successfully"
            });
        }
        
        public ContractsController(
            IContractRepository contractRepository, 
            IUserRepository userRepository, 
            IGroupRepository groupRepository,
            IContractAggregationService aggregationService,
            IUserMatriculaRepository userMatriculaRepository,
            IMatriculaRepository matriculaRepository,
            IMessageService messageService,
            IUserScopeService userScopeService,
            IExportService exportService,
            IContractStatusMapper statusMapper,
            IContractStatusService statusService,
            IPendingContractClaimRepository pendingClaimRepository)
        {
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _aggregationService = aggregationService;
            _userMatriculaRepository = userMatriculaRepository;
            _matriculaRepository = matriculaRepository;
            _messageService = messageService;
            _userScopeService = userScopeService;
            _exportService = exportService;
            _statusMapper = statusMapper;
            _statusService = statusService;
            _pendingClaimRepository = pendingClaimRepository;
        }

        // ── Export endpoints ─────────────────────────────────────────────────

        /// <summary>
        /// Queue an async XLSX export. Returns a jobId immediately.
        /// The export respects the same scope/filters as GET /api/contracts.
        /// </summary>
        [HttpPost("export")]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<ExportJobResponse>>> StartExport([FromBody] ContractExportRequest request)
        {
            var scope = await _userScopeService.GetContractScopeAsync(User);
            var requestingUserId = GetCurrentUserId().ToString();
            var jobId = _exportService.StartExport(request, scope, requestingUserId);
            var status = _exportService.GetJobStatus(jobId)!;
            return Ok(new ApiResponse<ExportJobResponse> { Success = true, Data = status, Message = "Export started" });
        }

        /// <summary>
        /// Poll export job status. Returns null when job has expired.
        /// </summary>
        [HttpGet("export/{jobId}")]
        [HasPermission("contracts:read")]
        public ActionResult<ApiResponse<ExportJobResponse>> GetExportStatus(string jobId)
        {
            var status = _exportService.GetJobStatus(jobId);
            if (status == null)
                return NotFound(new ApiResponse<ExportJobResponse> { Success = false, Message = "Job not found or expired" });
            return Ok(new ApiResponse<ExportJobResponse> { Success = true, Data = status, Message = "OK" });
        }

        /// <summary>
        /// Download the completed XLSX file.
        /// </summary>
        [HttpGet("export/{jobId}/download")]
        [HasPermission("contracts:read")]
        public ActionResult DownloadExport(string jobId)
        {
            var requestingUserId = GetCurrentUserId().ToString();
            var bytes = _exportService.GetJobBytes(jobId, requestingUserId);
            if (bytes == null)
                return NotFound(new { success = false, message = "File not ready, expired, or not found" });

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"contratos_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }


        
        [HttpGet]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetContracts(
            [FromQuery] Guid? userId = null,
            [FromQuery] int? groupId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? contractNumber = null,
            [FromQuery] bool? showUnassigned = null,
            [FromQuery] string? matricula = null,
            [FromQuery] string? userEmail = null)
        {
            var scope = await _userScopeService.GetContractScopeAsync(User);
            var contracts = await _contractRepository.GetAllAsync(userId, groupId, startDate, endDate, contractNumber, showUnassigned, matricula, userEmail, scope);
            
            var contractResponses = contracts.Select(MapToContractResponse).ToList();
            
            // Calculate aggregations using service
            var aggregation = _aggregationService.CalculateAggregation(contracts);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = contractResponses,
                Message = _messageService.Get(AppMessage.ContractsRetrievedSuccessfully),
                Aggregation = aggregation
            });
        }
        
        [HttpGet("user/{userId}")]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetUserContracts(
            Guid userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? matricula = null)
        {
            var currentUserId = GetCurrentUserId();
            var hasReadPermission = User.HasClaim("perm", "contracts:read") || User.HasClaim("perm", "system:superadmin");
            
            if (!hasReadPermission && currentUserId != userId)
            {
                return Forbid();
            }
            
            var contracts = await _contractRepository.GetByUserIdAsync(userId, startDate, endDate, matricula);
            
            var contractResponses = contracts.Select(MapToContractResponse).ToList();
            
            // Calculate aggregations using service
            var aggregation = _aggregationService.CalculateAggregation(contracts);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = contractResponses,
                Message = _messageService.Get(AppMessage.ContractsRetrievedSuccessfully),
                Aggregation = aggregation
            });
        }
        
        [HttpGet("aggregation/historic-production")]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<HistoricProductionResponse>>> GetHistoricProduction(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] bool? showUnassigned = null)
        {
            var currentUserId = GetCurrentUserId();
            var hasReadPermission = User.HasClaim("perm", "contracts:read") || User.HasClaim("perm", "system:superadmin");
            
            // If userId is specified and user does not have read-all permission, verify it's their own data
            if (userId.HasValue && !hasReadPermission && currentUserId != userId.Value)
            {
                return Forbid();
            }
            
            // ✅ Push grouping to database instead of loading all contracts into memory
            var monthlyData = await _contractRepository.GetMonthlyProductionAsync(userId, startDate, endDate, showUnassigned);
            
            var response = new HistoricProductionResponse
            {
                MonthlyData = monthlyData,
                TotalProduction = monthlyData.Sum(m => m.TotalProduction),
                TotalContracts = monthlyData.Sum(m => m.ContractCount)
            };
            
            return Ok(new ApiResponse<HistoricProductionResponse>
            {
                Success = true,
                Data = response,
                Message = _messageService.Get(AppMessage.HistoricProductionRetrievedSuccessfully)
            });
        }
        
        [HttpGet("{id}")]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> GetContract(int id)
        {
            var contract = await _contractRepository.GetByIdAsync(id);
            if (contract == null || !contract.IsActive)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ContractNotFound)
                });
            }
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = _messageService.Get(AppMessage.ContractRetrievedSuccessfully)
            });
        }
        
        [HttpGet("number/{contractNumber}")]
        [HasPermission("contracts:read")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> GetContractByNumber(string contractNumber)
        {
            var contract = await _contractRepository.GetByContractNumberAsync(contractNumber);
            if (contract == null || !contract.IsActive)
            {
                // Check if another user has already claimed it
                var existingClaim = await _pendingClaimRepository.GetByContractNumberAsync(contractNumber);
                if (existingClaim != null)
                {
                    // Find the user who claimed it
                    var claimUser = existingClaim.User ?? await _userRepository.GetByIdAsync(existingClaim.User?.Id ?? Guid.Empty);
                    if (claimUser != null)
                    {
                        return NotFound(new 
                        {
                            success = false,
                            message = $"O contrato {contractNumber} já foi solicitado por {claimUser.Name} ({claimUser.Email}). Para assumir este contrato, o usuário atual deve cancelar a solicitação.",
                            notFoundYet = true,
                            alreadyClaimed = true
                        });
                    }
                }

                return NotFound(new 
                {
                    success = false,
                    message = "ContractNotFoundYet",
                    notFoundYet = true
                });
            }
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = _messageService.Get(AppMessage.ContractRetrievedSuccessfully)
            });
        }
        
        [HttpPost]
        [HasPermission("contracts:create")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> CreateContract(ContractRequest request)
        {
            // Validate contract number doesn't already exist (active contracts only)
            var existingContract = await _contractRepository.GetByContractNumberAsync(request.ContractNumber);
            if (existingContract != null)
            {
                if (existingContract.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.ContractNumberAlreadyExists)
                    });
                }

                // Restore a soft-deleted contract with the new request data
                existingContract.IsActive = true;
                existingContract.TotalAmount = request.TotalAmount;
                existingContract.ContractStatusId = await _statusService.GetStatusIdByNameAsync(request.Status);
                existingContract.SaleStartDate = request.ContractStartDate;
                if (request.UserId.HasValue)
                {
                    var assignUser = await _userRepository.GetByIdAsync(request.UserId.Value);
                    existingContract.UserInternalId = assignUser?.InternalId;
                }
                existingContract.GroupId = request.GroupId;
                existingContract.CustomerName = request.CustomerName;
                existingContract.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(request.MatriculaNumber))
                {
                    var normalizedMatricula = Utils.NormalizationUtils.NormalizeNumber(request.MatriculaNumber);
                    var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(normalizedMatricula);
                    if (matricula == null)
                    {
                        matricula = new Matricula { MatriculaNumber = normalizedMatricula, StartDate = DateTime.UtcNow, Status = "active" };
                        await _matriculaRepository.CreateAsync(matricula);
                    }
                    existingContract.MatriculaId = matricula.Id;
                    existingContract.TempMatricula = matricula.MatriculaNumber;
                }

                await _contractRepository.UpdateAsync(existingContract);
                return Ok(new ApiResponse<ContractResponse>
                {
                    Success = true,
                    Data = MapToContractResponse(existingContract),
                    Message = _messageService.Get(AppMessage.ContractCreatedSuccessfully)
                });
            }
            
            // Validate user exists (if provided)
            if (request.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(request.UserId.Value);
                if (user == null || !user.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.UserNotFound)
                    });
                }
            }
            
            // Validate status
            if (!_statusMapper.IsValidStatus(request.Status))
            {
                return BadRequest(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = $"Invalid status. Must be one of: {string.Join(", ", _statusMapper.GetValidStatuses())}"
                });
            }
            
            // Validate and convert ContractType if provided
            int? contractTypeInt = null;
            if (!string.IsNullOrWhiteSpace(request.ContractType))
            {
                try
                {
                    contractTypeInt = ContractTypeExtensions.FromApiStringToInt(request.ContractType);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = ex.Message
                    });
                }
            }
            
            
            // Validate group exists (if provided)
            if (request.GroupId.HasValue)
            {
                var group = await _groupRepository.GetByIdAsync(request.GroupId.Value);
                if (group == null || !group.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Invalid group"
                    });
                }
            }
            
            var contract = new Contract
            {
                ContractNumber = request.ContractNumber,
                TotalAmount = request.TotalAmount,
                GroupId = request.GroupId,
                ContractStatusId = await _statusService.GetStatusIdByNameAsync(request.Status),
                SaleStartDate = request.ContractStartDate,
                ContractType = contractTypeInt,
                Quota = request.Quota,
                PvId = request.PvId,
                CustomerName = request.CustomerName
            };

            // Set user by InternalId if provided
            if (request.UserId.HasValue)
            {
                var assignUser = await _userRepository.GetByIdAsync(request.UserId.Value);
                if (assignUser != null) contract.UserInternalId = assignUser.InternalId;
            }

            // Matricula validation
            if (!string.IsNullOrEmpty(request.MatriculaNumber))
            {
                var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
                if (matricula != null)
                {
                    // If it exists, check if it belongs to the contract's user
                    if (request.UserId.HasValue)
                    {
                        var isAssignedToUser = await _userMatriculaRepository.GetByMatriculaNumberAndUserIdAsync(request.MatriculaNumber, request.UserId.Value);
                        if (isAssignedToUser == null)
                        {
                            return BadRequest(new ApiResponse<ContractResponse> { Success = false, Message = "Matrícula not found for this user" });
                        }
                    }
                }
                else
                {
                    // Create matricula if it doesn't exist (allowing unassigned contracts with a trace)
                    matricula = new Matricula
                    {
                        MatriculaNumber = request.MatriculaNumber,
                        StartDate = DateTime.UtcNow,
                        Status = "active"
                    };
                    await _matriculaRepository.CreateAsync(matricula);
                }
                
                contract.MatriculaId = matricula.Id;
                contract.TempMatricula = matricula.MatriculaNumber;
            }
            else if (request.UserMatriculaId.HasValue)
            {
                // Legacy support for UserMatriculaId if needed, but we prefer MatriculaId
                var um = await _userMatriculaRepository.GetByIdAsync(request.UserMatriculaId.Value);
                if (um != null)
                {
                    contract.MatriculaId = um.MatriculaId;
                    contract.TempMatricula = um.Matricula?.MatriculaNumber;
                }
            }

            
            await _contractRepository.CreateAsync(contract);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = _messageService.Get(AppMessage.ContractCreatedSuccessfully)
            });
        }
        
        [HttpPut("{id}")]
        [HasPermission("contracts:update")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> UpdateContract(int id, UpdateContractRequest request)
        {
            // Normalize inputs
            request.ContractNumber = NormalizationUtils.NormalizeNumber(request.ContractNumber);
            request.MatriculaNumber = NormalizationUtils.NormalizeNumber(request.MatriculaNumber);

            var contract = await _contractRepository.GetByIdAsync(id);
            if (contract == null || !contract.IsActive)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ContractNotFound)
                });
            }
            
            if (!string.IsNullOrEmpty(request.ContractNumber))
            {
                // Validate contract number doesn't already exist (excluding current contract)
                var existingContract = await _contractRepository.GetByContractNumberAsync(request.ContractNumber);
                if (existingContract != null && existingContract.Id != id)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.ContractNumberAlreadyExists)
                    });
                }
                contract.ContractNumber = request.ContractNumber;
            }
            
            // Always update UserId (allows clearing)
            if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
            {
                var user = await _userRepository.GetByIdAsync(request.UserId.Value);
                if (user == null || !user.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.UserNotFound)
                    });
                }
                contract.UserInternalId = user.InternalId;
            }
            else if (request.UserId.HasValue && request.UserId.Value == Guid.Empty)
            {
                contract.UserInternalId = null;
            }
            
            // Always update GroupId
            if (request.GroupId.HasValue && request.GroupId.Value != 0)
            {
                var group = await _groupRepository.GetByIdAsync(request.GroupId.Value);
                if (group == null || !group.IsActive)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Invalid group"
                    });
                }
                contract.GroupId = request.GroupId.Value;
            }
            else if (request.GroupId.HasValue && request.GroupId.Value == 0)
            {
                contract.GroupId = null;
            }
            
            if (request.TotalAmount.HasValue)
                contract.TotalAmount = request.TotalAmount.Value;
                
            if (!string.IsNullOrEmpty(request.Status))
            {
                if (!_statusMapper.IsValidStatus(request.Status))
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = $"Invalid status. Must be one of: {string.Join(", ", _statusMapper.GetValidStatuses())}"
                    });
                }
                contract.ContractStatusId = await _statusService.GetStatusIdByNameAsync(request.Status);
            }
                
            if (request.ContractStartDate.HasValue)
                contract.SaleStartDate = request.ContractStartDate.Value;
                
            if (request.IsActive.HasValue)
                contract.IsActive = request.IsActive.Value;
                
            if (!string.IsNullOrWhiteSpace(request.ContractType))
            {
                try
                {
                    contract.ContractType = ContractTypeExtensions.FromApiStringToInt(request.ContractType);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = ex.Message
                    });
                }
            }
                
            
            if (request.Quota.HasValue)
                contract.Quota = request.Quota.Value;
                
            // Always update PvId
            if (request.PvId.HasValue && request.PvId.Value != 0)
            {
                contract.PvId = request.PvId.Value;
            }
            else if (request.PvId.HasValue && request.PvId.Value == 0)
            {
                contract.PvId = null;
            }

            contract.CustomerName = request.CustomerName;

            // Matricula validation
            if (!string.IsNullOrEmpty(request.MatriculaNumber))
            {
                var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(request.MatriculaNumber);
                if (matricula != null)
                {
                    // If it exists, check if it belongs to the contract's user
                    var contractUserGuid = contract.User?.Id;
                    if (contractUserGuid.HasValue)
                    {
                        var isAssignedToUser = await _userMatriculaRepository.GetByMatriculaNumberAndUserIdAsync(request.MatriculaNumber, contractUserGuid.Value);
                        if (isAssignedToUser == null)
                        {
                            return BadRequest(new ApiResponse<ContractResponse> { Success = false, Message = "Matrícula not found for this user" });
                        }
                    }
                }
                else
                {
                    matricula = new Matricula
                    {
                        MatriculaNumber = request.MatriculaNumber,
                        StartDate = DateTime.UtcNow,
                        Status = "active"
                    };
                    await _matriculaRepository.CreateAsync(matricula);
                }
                contract.MatriculaId = matricula.Id;
                contract.TempMatricula = matricula.MatriculaNumber;
            }
            else
            {
                // Handle UserMatriculaId (legacy)
                if (request.UserMatriculaId.HasValue)
                {
                    var um = await _userMatriculaRepository.GetByIdAsync(request.UserMatriculaId.Value);
                    if (um != null)
                    {
                        contract.MatriculaId = um.MatriculaId;
                        contract.TempMatricula = um.Matricula?.MatriculaNumber;
                    }
                }
            }
            
            if (request.IsActive.HasValue)
                contract.IsActive = request.IsActive.Value;
            
            contract.UpdatedAt = DateTime.UtcNow;
            
            await _contractRepository.UpdateAsync(contract);
            
            // Reload contract with all relationships to get updated MatriculaNumber
            var updatedContract = await _contractRepository.GetByIdAsync(id);            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(updatedContract!),
                Message = _messageService.Get(AppMessage.ContractUpdatedSuccessfully)
            });
        }
        
        [HttpDelete("{id}")]
        [HasPermission("contracts:delete")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteContract(int id)
        {
            var contract = await _contractRepository.GetByIdAsync(id);
            if (contract == null || !contract.IsActive)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ContractNotFound)
                });
            }
            
            contract.IsActive = false;
            await _contractRepository.UpdateAsync(contract);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.ContractDeletedSuccessfully)
            });
        }

        // ── Pending Claims ─────────────────────────────────────────────────

        [HttpPost("claims")]
        public async Task<ActionResult<ApiResponse<PendingClaimResponse>>> CreateClaim(PendingClaimRequest request)
        {
            request.ContractNumber = NormalizationUtils.NormalizeNumber(request.ContractNumber);
            var currentUserId = GetCurrentUserId();

            // 1. Verify contract isn't already active
            var existingContract = await _contractRepository.GetByContractNumberAsync(request.ContractNumber);
            if (existingContract != null && existingContract.IsActive)
            {
                return BadRequest(new { success = false, message = "Contrato já existe no sistema." });
            }

            // 2. Resolve the UserMatricula join record (verifies both ownership and MatriculaId in one step)
            var userMatricula = await _userMatriculaRepository.GetByIdAsync(request.UserMatriculaId);
            if (userMatricula == null || userMatricula.User?.Id != currentUserId || !userMatricula.IsActive)
            {
                return BadRequest(new { success = false, message = "Matrícula inválida ou não pertence ao usuário." });
            }

            var resolvedMatriculaId = userMatricula.MatriculaId;

            // 3. Check for existing UNRESOLVED claim
            var unresolvedClaims = await _pendingClaimRepository.GetUnresolvedByContractNumbersAsync(new List<string> { request.ContractNumber });
            var existingClaim = unresolvedClaims.FirstOrDefault();
            
            if (existingClaim != null)
            {
                if (existingClaim.User?.Id == currentUserId)
                {
                    return Ok(new ApiResponse<PendingClaimResponse> { Success = true, Message = "Reivindicação já registrada.", Data = MapToPendingClaimResponse(existingClaim) });
                }

                var claimUser = existingClaim.User ?? await _userRepository.GetByIdAsync(existingClaim.User?.Id ?? Guid.Empty);
                return BadRequest(new { 
                    success = false, 
                    message = $"O contrato {request.ContractNumber} já foi solicitado por {claimUser?.Name} ({claimUser?.Email}). Para assumir este contrato, o usuário atual deve cancelar a solicitação." 
                });
            }

            var currentUser = await _userRepository.GetByIdAsync(currentUserId);
            if (currentUser == null)
                return BadRequest(new { success = false, message = "Usuário não encontrado." });

            var claim = new PendingContractClaim
            {
                ContractNumber = request.ContractNumber.Trim(),
                UserInternalId = currentUser.InternalId,
                MatriculaId = resolvedMatriculaId
            };

            await _pendingClaimRepository.CreateAsync(claim);
            
            // Refetch to include relationships for the response
            var created = await _pendingClaimRepository.GetByContractNumberAsync(request.ContractNumber);

            return Created("", new ApiResponse<PendingClaimResponse>
            {
                Success = true,
                Message = "Solicitação de contrato registrada com sucesso.",
                Data = MapToPendingClaimResponse(created!)
            });
        }

        [HttpGet("claims")]
        public async Task<ActionResult<ApiResponse<List<PendingClaimResponse>>>> GetMyClaims()
        {
            var currentUserId = GetCurrentUserId();
            var claims = await _pendingClaimRepository.GetUnresolvedByUserIdAsync(currentUserId);

            return Ok(new ApiResponse<List<PendingClaimResponse>>
            {
                Success = true,
                Data = claims.Select(MapToPendingClaimResponse).ToList(),
                Message = "Solicitações recuperadas com sucesso."
            });
        }

        [HttpGet("claims/matricula/{matriculaId}")]
        public async Task<ActionResult<ApiResponse<List<PendingClaimResponse>>>> GetClaimsByMatricula(int matriculaId)
        {
            var currentUserId = GetCurrentUserId();
            var hasReadPermission = User.HasClaim("perm", "contracts:read") || User.HasClaim("perm", "system:superadmin");

            // Verify user is owner of this matricula OR has read-all permissions
            var ownerLink = await _userMatriculaRepository.GetOwnerByMatriculaIdAsync(matriculaId);
            
            if (!hasReadPermission && (ownerLink == null || ownerLink.User?.Id != currentUserId))
            {
                return Forbid();
            }

            var claims = await _pendingClaimRepository.GetUnresolvedByMatriculaIdAsync(matriculaId);

            return Ok(new ApiResponse<List<PendingClaimResponse>>
            {
                Success = true,
                Data = claims.Select(MapToPendingClaimResponse).ToList(),
                Message = "Solicitações da matrícula recuperadas com sucesso."
            });
        }

        [HttpDelete("claims/{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteClaim(int id)
        {
            var currentUserId = GetCurrentUserId();
            var isSuperAdmin = User.HasClaim("perm", "system:superadmin");
            
            var claim = await _pendingClaimRepository.GetByIdAsync(id);
            if (claim == null) return NotFound();

            if (!isSuperAdmin && claim.User?.Id != currentUserId)
            {
                return Forbid();
            }

            await _pendingClaimRepository.DeleteAsync(claim);

            return Ok(new ApiResponse<object> { Success = true, Message = "Solicitação excluída com sucesso." });
        }

        [HttpDelete("claims/number/{contractNumber}")]
        [HasPermission("contracts:delete")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteClaimsByNumber(string contractNumber)
        {
            await _pendingClaimRepository.DeleteByContractNumberAsync(contractNumber);
            return Ok(new ApiResponse<object> { Success = true, Message = "Solicitações excluídas." });
        }

        private PendingClaimResponse MapToPendingClaimResponse(PendingContractClaim claim)
        {
            return new PendingClaimResponse
            {
                Id = claim.Id,
                ContractNumber = claim.ContractNumber,
                UserId = claim.User?.Id ?? Guid.Empty,
                UserName = claim.User?.Name ?? "",
                UserEmail = claim.User?.Email ?? "",
                MatriculaId = claim.MatriculaId,
                MatriculaNumber = claim.Matricula?.MatriculaNumber ?? "",
                ClaimedAt = claim.ClaimedAt,
                IsResolved = claim.IsResolved,
                ResolvedAt = claim.ResolvedAt
            };
        }
        
        private ContractResponse MapToContractResponse(Contract contract)
        {
            // Resolve the most appropriate matricula number for the response
            var matriculaNumber = contract.Matricula?.MatriculaNumber ?? contract.TempMatricula;

            return new ContractResponse
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                UserId = contract.User?.Id,
                UserName = contract.User?.Name ?? "",
                TotalAmount = contract.TotalAmount,
                GroupId = contract.GroupId,
                GroupName = contract.Group?.Name ?? "",
                Status = contract.ContractStatus?.Name ?? "",
                ContractStartDate = contract.SaleStartDate,
                IsActive = contract.IsActive,
                CreatedAt = contract.CreatedAt,
                UpdatedAt = contract.UpdatedAt,
                ContractType = ContractTypeExtensions.ToApiString(contract.ContractType),
                Quota = contract.Quota,
                PvId = contract.PvId,
                CustomerName = contract.CustomerName,
                MatriculaId = contract.MatriculaId,
                MatriculaNumber = matriculaNumber
            };
        }
        
    
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
        
        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.User;
        }
    }
}