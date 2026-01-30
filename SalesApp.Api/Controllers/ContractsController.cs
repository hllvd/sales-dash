using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
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
        private readonly IUserMatriculaRepository _matriculaRepository;
        private readonly IMessageService _messageService;
        
        public ContractsController(
            IContractRepository contractRepository, 
            IUserRepository userRepository, 
            IGroupRepository groupRepository,
            IContractAggregationService aggregationService,
            IUserMatriculaRepository matriculaRepository,
            IMessageService messageService)
        {
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _aggregationService = aggregationService;
            _matriculaRepository = matriculaRepository;
            _messageService = messageService;
        }
        
        [HttpGet]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetContracts(
            [FromQuery] Guid? userId = null,
            [FromQuery] int? groupId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var contracts = await _contractRepository.GetAllAsync(userId, groupId, startDate, endDate);
            
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
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetUserContracts(
            Guid userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserRole != "superadmin" && currentUserId != userId)
            {
                return Forbid();
            }
            
            var contracts = await _contractRepository.GetByUserIdAsync(userId, startDate, endDate);
            
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
        public async Task<ActionResult<ApiResponse<HistoricProductionResponse>>> GetHistoricProduction(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? userId = null)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            // If userId is specified and user is not admin/superadmin, verify it's their own data
            if (userId.HasValue && currentUserRole != "admin" && currentUserRole != "superadmin" && currentUserId != userId.Value)
            {
                return Forbid();
            }
            
            // ✅ Push grouping to database instead of loading all contracts into memory
            var monthlyData = await _contractRepository.GetMonthlyProductionAsync(userId, startDate, endDate);
            
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
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> GetContractByNumber(string contractNumber)
        {
            var contract = await _contractRepository.GetByContractNumberAsync(contractNumber);
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
        
        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> CreateContract(ContractRequest request)
        {
            // Validate contract number doesn't already exist
            var existingContract = await _contractRepository.GetByContractNumberAsync(request.ContractNumber);
            if (existingContract != null)
            {
                return BadRequest(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.ContractNumberAlreadyExists)
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
            if (!Services.ContractStatusMapper.IsValidStatus(request.Status))
            {
                return BadRequest(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = $"Invalid status. Must be one of: {string.Join(", ", Services.ContractStatusMapper.GetValidStatuses())}"
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
                UserId = request.UserId,
                TotalAmount = request.TotalAmount,
                GroupId = request.GroupId,
                Status = request.Status,
                SaleStartDate = request.ContractStartDate,
                ContractType = contractTypeInt,
                Quota = request.Quota,
                PvId = request.PvId,
                CustomerName = request.CustomerName
            };

            
            await _contractRepository.CreateAsync(contract);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = _messageService.Get(AppMessage.ContractCreatedSuccessfully)
            });
        }
        
        [HttpPut("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> UpdateContract(int id, UpdateContractRequest request)
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
                contract.UserId = request.UserId.Value;
            }
            
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
                contract.GroupId = request.GroupId.Value;
            }
            
            if (request.TotalAmount.HasValue)
                contract.TotalAmount = request.TotalAmount.Value;
                
            if (!string.IsNullOrEmpty(request.Status))
            {
                if (!Services.ContractStatusMapper.IsValidStatus(request.Status))
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = $"Invalid status. Must be one of: {string.Join(", ", Services.ContractStatusMapper.GetValidStatuses())}"
                    });
                }
                contract.Status = request.Status;
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
                
            if (request.PvId.HasValue) contract.PvId = request.PvId.Value;
            if (!string.IsNullOrEmpty(request.CustomerName)) contract.CustomerName = request.CustomerName;
            
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
        [Authorize(Roles = "superadmin")]
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
        
        private ContractResponse MapToContractResponse(Contract contract)
        {
            // Resolve matricula from user's matriculas (picking owner or latest active)
            var userMatricula = contract.User?.UserMatriculas?
                .OrderByDescending(m => m.IsOwner)
                .ThenByDescending(m => m.StartDate)
                .FirstOrDefault(m => m.IsActive);

            return new ContractResponse
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                UserId = contract.UserId,
                UserName = contract.User?.Name ?? "",
                TotalAmount = contract.TotalAmount,
                GroupId = contract.GroupId,
                GroupName = contract.Group?.Name ?? "",
                Status = contract.Status,
                ContractStartDate = contract.SaleStartDate,
                IsActive = contract.IsActive,
                CreatedAt = contract.CreatedAt,
                UpdatedAt = contract.UpdatedAt,
                ContractType = ContractTypeExtensions.ToApiString(contract.ContractType),
                Quota = contract.Quota,
                PvId = contract.PvId,
                CustomerName = contract.CustomerName,
                MatriculaId = userMatricula?.Id,
                MatriculaNumber = userMatricula?.MatriculaNumber
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