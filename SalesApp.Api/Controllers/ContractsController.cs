using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
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
        
        public ContractsController(IContractRepository contractRepository, IUserRepository userRepository, IGroupRepository groupRepository)
        {
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
        }
        
        [HttpGet]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetContracts(
            [FromQuery] Guid? userId = null,
            [FromQuery] Guid? groupId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var contracts = await _contractRepository.GetAllAsync(userId, groupId, startDate, endDate);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = contracts.Select(MapToContractResponse).ToList(),
                Message = "Contracts retrieved successfully"
            });
        }
        
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetUserContracts(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserRole != "superadmin" && currentUserId != userId)
            {
                return Forbid();
            }
            
            var contracts = await _contractRepository.GetByUserIdAsync(userId);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = contracts.Select(MapToContractResponse).ToList(),
                Message = "User contracts retrieved successfully"
            });
        }
        
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,superadmin")]
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
            // Validate user exists
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "Invalid user"
                });
            }
            
            // Validate group exists
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null || !group.IsActive)
            {
                return BadRequest(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "Invalid group"
                });
            }
            
            var contract = new Contract
            {
                ContractNumber = request.ContractNumber,
                UserId = request.UserId,
                TotalAmount = request.TotalAmount,
                GroupId = request.GroupId,
                Status = request.Status,
                SaleStartDate = request.ContractStartDate,
                SaleEndDate = request.ContractEndDate
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
        [Authorize(Roles = "admin,superadmin")]
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
                contract.ContractNumber = request.ContractNumber;
                
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
                contract.Status = request.Status;
                
            if (request.ContractStartDate.HasValue)
                contract.SaleStartDate = request.ContractStartDate.Value;
                
            if (request.ContractEndDate.HasValue)
                contract.SaleEndDate = request.ContractEndDate.Value;
                
            if (request.IsActive.HasValue)
                contract.IsActive = request.IsActive.Value;
            
            await _contractRepository.UpdateAsync(contract);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToContractResponse(contract),
                Message = "Contract updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin,superadmin")]
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
                ContractEndDate = contract.SaleEndDate,
                IsActive = contract.IsActive,
                CreatedAt = contract.CreatedAt,
                UpdatedAt = contract.UpdatedAt
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