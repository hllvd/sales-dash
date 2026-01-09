using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Attributes;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,superadmin")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IMessageService _messageService;
        
        public RolesController(IRoleRepository roleRepository, IMessageService messageService)
        {
            _roleRepository = roleRepository;
            _messageService = messageService;
        }
        
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<RoleResponse>>>> GetRoles()
        {
            var roles = await _roleRepository.GetAllAsync();
            
            return Ok(new ApiResponse<List<RoleResponse>>
            {
                Success = true,
                Data = roles.Select(MapToRoleResponse).ToList(),
                Message = _messageService.Get(AppMessage.RolesRetrievedSuccessfully)
            });
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<RoleResponse>>> GetRole(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role == null || !role.IsActive)
            {
                return NotFound(new ApiResponse<RoleResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.RoleNotFound)
                });
            }
            
            return Ok(new ApiResponse<RoleResponse>
            {
                Success = true,
                Data = MapToRoleResponse(role),
                Message = _messageService.Get(AppMessage.RoleRetrievedSuccessfully)
            });
        }
        
        [HttpPost]
        public async Task<ActionResult<ApiResponse<RoleResponse>>> CreateRole(RoleRequest request)
        {
            if (await _roleRepository.NameExistsAsync(request.Name))
            {
                return BadRequest(new ApiResponse<RoleResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.RoleNameAlreadyExists)
                });
            }
            
            var role = new Role
            {
                Name = request.Name,
                Description = request.Description,
                Level = request.Level,
                Permissions = request.Permissions
            };
            
            await _roleRepository.CreateAsync(role);
            
            return Ok(new ApiResponse<RoleResponse>
            {
                Success = true,
                Data = MapToRoleResponse(role),
                Message = _messageService.Get(AppMessage.RoleCreatedSuccessfully)
            });
        }
        
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<RoleResponse>>> UpdateRole(int id, UpdateRoleRequest request)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role == null || !role.IsActive)
            {
                return NotFound(new ApiResponse<RoleResponse>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.RoleNotFound)
                });
            }
            
            if (!string.IsNullOrEmpty(request.Name))
            {
                if (await _roleRepository.NameExistsAsync(request.Name, id))
                {
                    return BadRequest(new ApiResponse<RoleResponse>
                    {
                        Success = false,
                        Message = _messageService.Get(AppMessage.RoleNameAlreadyExists)
                    });
                }
                role.Name = request.Name;
            }
            
            if (!string.IsNullOrEmpty(request.Description))
                role.Description = request.Description;
                
            if (request.Level.HasValue)
                role.Level = request.Level.Value;
                
            if (request.Permissions != null)
                role.Permissions = request.Permissions;
                
            if (request.IsActive.HasValue)
                role.IsActive = request.IsActive.Value;
            
            await _roleRepository.UpdateAsync(role);
            
            return Ok(new ApiResponse<RoleResponse>
            {
                Success = true,
                Data = MapToRoleResponse(role),
                Message = _messageService.Get(AppMessage.RoleUpdatedSuccessfully)
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteRole(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role == null || !role.IsActive)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.RoleNotFound)
                });
            }
            
            await _roleRepository.DeleteAsync(id);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = _messageService.Get(AppMessage.RoleDeletedSuccessfully)
            });
        }
        
        private RoleResponse MapToRoleResponse(Role role)
        {
            return new RoleResponse
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Level = role.Level,
                Permissions = role.Permissions,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt
            };
        }
    }
}