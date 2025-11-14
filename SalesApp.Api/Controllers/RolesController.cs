using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Attributes;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,superadmin")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleRepository _roleRepository;
        
        public RolesController(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }
        
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<RoleResponse>>>> GetRoles()
        {
            var roles = await _roleRepository.GetAllAsync();
            
            return Ok(new ApiResponse<List<RoleResponse>>
            {
                Success = true,
                Data = roles.Select(MapToRoleResponse).ToList(),
                Message = "Roles retrieved successfully"
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
                    Message = "Role not found"
                });
            }
            
            return Ok(new ApiResponse<RoleResponse>
            {
                Success = true,
                Data = MapToRoleResponse(role),
                Message = "Role retrieved successfully"
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
                    Message = "Role name already exists"
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
                Message = "Role created successfully"
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
                    Message = "Role not found"
                });
            }
            
            if (!string.IsNullOrEmpty(request.Name))
            {
                if (await _roleRepository.NameExistsAsync(request.Name, id))
                {
                    return BadRequest(new ApiResponse<RoleResponse>
                    {
                        Success = false,
                        Message = "Role name already exists"
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
                Message = "Role updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteRole(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role == null || !role.IsActive)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Role not found"
                });
            }
            
            await _roleRepository.DeleteAsync(id);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Role deleted successfully"
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