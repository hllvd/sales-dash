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
    public class SalesController : ControllerBase
    {
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        
        public SalesController(IContractRepository contractRepository, IUserRepository userRepository, IGroupRepository groupRepository)
        {
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
        }
        
        [HttpGet]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetSales(
            [FromQuery] Guid? userId = null,
            [FromQuery] Guid? groupId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var sales = await _contractRepository.GetAllAsync(userId, groupId, startDate, endDate);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = sales.Select(MapToSaleResponse).ToList(),
                Message = "Sales retrieved successfully"
            });
        }
        
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ApiResponse<List<ContractResponse>>>> GetUserSales(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();
            
            if (currentUserRole != "admin" && currentUserRole != "superadmin" && currentUserId != userId)
            {
                return Forbid();
            }
            
            var sales = await _contractRepository.GetByUserIdAsync(userId);
            
            return Ok(new ApiResponse<List<ContractResponse>>
            {
                Success = true,
                Data = sales.Select(MapToSaleResponse).ToList(),
                Message = "User sales retrieved successfully"
            });
        }
        
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> GetSale(Guid id)
        {
            var sale = await _contractRepository.GetByIdAsync(id);
            if (sale == null || !sale.IsActive)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "Sale not found"
                });
            }
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToSaleResponse(sale),
                Message = "Sale retrieved successfully"
            });
        }
        
        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> CreateSale(ContractRequest request)
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
            
            var sale = new Contract
            {
                UserId = request.UserId,
                TotalAmount = request.TotalAmount,
                GroupId = request.GroupId,
                Status = request.Status,
                SaleStartDate = request.ContractStartDate,
                SaleEndDate = request.ContractEndDate
            };
            
            await _contractRepository.CreateAsync(sale);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToSaleResponse(sale),
                Message = "Sale created successfully"
            });
        }
        
        [HttpPut("{id}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<ContractResponse>>> UpdateSale(Guid id, UpdateSaleRequest request)
        {
            var sale = await _contractRepository.GetByIdAsync(id);
            if (sale == null || !sale.IsActive)
            {
                return NotFound(new ApiResponse<ContractResponse>
                {
                    Success = false,
                    Message = "Sale not found"
                });
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
                sale.UserId = request.UserId.Value;
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
                sale.GroupId = request.GroupId.Value;
            }
            
            if (request.TotalAmount.HasValue)
                sale.TotalAmount = request.TotalAmount.Value;
                
            if (!string.IsNullOrEmpty(request.Status))
                sale.Status = request.Status;
                
            if (request.ContractStartDate.HasValue)
                sale.SaleStartDate = request.ContractStartDate.Value;
                
            if (request.ContractEndDate.HasValue)
                sale.SaleEndDate = request.ContractEndDate.Value;
                
            if (request.IsActive.HasValue)
                sale.IsActive = request.IsActive.Value;
            
            await _contractRepository.UpdateAsync(sale);
            
            return Ok(new ApiResponse<ContractResponse>
            {
                Success = true,
                Data = MapToSaleResponse(sale),
                Message = "Sale updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteSale(Guid id)
        {
            var sale = await _contractRepository.GetByIdAsync(id);
            if (sale == null || !sale.IsActive)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Sale not found"
                });
            }
            
            sale.IsActive = false;
            await _contractRepository.UpdateAsync(sale);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Sale deleted successfully"
            });
        }
        
        private ContractResponse MapToSaleResponse(Contract sale)
        {
            return new ContractResponse
            {
                Id = sale.Id,
                UserId = sale.UserId,
                UserName = sale.User?.Name ?? "",
                TotalAmount = sale.TotalAmount,
                GroupId = sale.GroupId,
                GroupName = sale.Group?.Name ?? "",
                Status = sale.Status,
                ContractStartDate = sale.SaleStartDate,
                ContractEndDate = sale.SaleEndDate,
                IsActive = sale.IsActive,
                CreatedAt = sale.CreatedAt,
                UpdatedAt = sale.UpdatedAt
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