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
        
        public ContractsController(
            IContractRepository contractRepository, 
            IUserRepository userRepository, 
            IGroupRepository groupRepository,
            IContractAggregationService aggregationService,
            IUserMatriculaRepository matriculaRepository)
        {
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _aggregationService = aggregationService;
            _matriculaRepository = matriculaRepository;
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
                Message = "Contracts retrieved successfully",
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
                Message = "User contracts retrieved successfully",
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
            
            // Get contracts with filters
            var contracts = await _contractRepository.GetAllAsync(userId, null, startDate, endDate);
            
            // Group by month and calculate production
            var monthlyData = contracts
                .GroupBy(c => new { Year = c.SaleStartDate.Year, Month = c.SaleStartDate.Month })
                .Select(g => new MonthlyProduction
                {
                    Period = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    TotalProduction = g.Sum(c => c.TotalAmount),
                    ContractCount = g.Count()
                })
                .OrderBy(m => m.Period)
                .ToList();
            
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
                Message = "Historic production retrieved successfully"
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
                    Message = "Contract not found"
                });
            }
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = "Contract retrieved successfully"
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
                    Message = "Contract not found"
                });
            }
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = "Contract retrieved successfully"
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
                    Message = "Contract number already exists"
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
                        Message = "Invalid user"
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
            
            // Validate and resolve matricula if provided
            int? matriculaId = null;
            if (!string.IsNullOrWhiteSpace(request.MatriculaNumber) && request.UserId.HasValue)
            {
                var (isValid, matricula, errorMessage) = await ValidateMatriculaForUser(request.MatriculaNumber, request.UserId.Value);
                if (!isValid)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = errorMessage ?? "Invalid matricula"
                    });
                }
                matriculaId = matricula!.Id;
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
                CustomerName = request.CustomerName,
                MatriculaId = matriculaId
            };

            
            await _contractRepository.CreateAsync(contract);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = "Contract created successfully"
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
                    Message = "Contract not found"
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
                        Message = "Contract number already exists"
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
                        Message = "Invalid user"
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
            
            // Validate and update matricula if provided
            if (!string.IsNullOrWhiteSpace(request.MatriculaNumber))
            {
                var userId = contract.UserId ?? Guid.Empty;
                if (userId == Guid.Empty)
                {
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = "Cannot assign matricula to contract without a user"
                    });
                }
                
                var (isValid, matricula, errorMessage) = await ValidateMatriculaForUser(request.MatriculaNumber, userId);
                if (!isValid)
                {
                    // Get user info for better error message
                    var user = await _userRepository.GetByIdAsync(userId);
                    var userName = user?.Name ?? "Unknown";
                    var enhancedMessage = $"{errorMessage} (Contract assigned to: {userName}, User ID: {userId})";
                    
                    return BadRequest(new ApiResponse<ContractResponse>
                    {
                        Success = false,
                        Message = enhancedMessage
                    });
                }
                contract.MatriculaId = matricula!.Id;
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
                Message = "Contract updated successfully"
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
                    Message = "Contract not found"
                });
            }
            
            contract.IsActive = false;
            await _contractRepository.UpdateAsync(contract);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Contract deleted successfully"
            });
        }
        
        private ContractResponse MapToContractResponse(Contract contract)
        {
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
                MatriculaId = contract.MatriculaId,
                MatriculaNumber = contract.UserMatricula?.MatriculaNumber
            };
        }
        
        private async Task<(bool isValid, UserMatricula? matricula, string? errorMessage)> 
            ValidateMatriculaForUser(string matriculaNumber, Guid userId)
        {
            // Query for matricula by BOTH number AND userId since multiple users can have the same number
            var matricula = await _matriculaRepository.GetByMatriculaNumberAndUserIdAsync(matriculaNumber, userId);
            
            if (matricula == null)
                return (false, null, $"Matricula '{matriculaNumber}' not found for this user");
            
            if (!matricula.IsActive)
                return (false, null, $"Matricula '{matriculaNumber}' is not active");
            
            return (true, matricula, null);
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