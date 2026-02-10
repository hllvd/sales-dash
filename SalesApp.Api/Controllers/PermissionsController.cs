using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Attributes;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [HasPermission("system:superadmin")]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMessageService _messageService;
        private readonly IRbacCache _rbacCache;

        public PermissionsController(AppDbContext context, IMessageService messageService, IRbacCache rbacCache)
        {
            _context = context;
            _messageService = messageService;
            _rbacCache = rbacCache;
        }

        [HttpGet("matrix")]
        public async Task<ActionResult<PermissionMatrixResponse>> GetMatrix()
        {
            var roles = await _context.Roles
                .Where(r => r.IsActive)
                .Select(r => new RoleMatrixDto { Id = r.Id, Name = r.Name })
                .ToListAsync();

            var permissions = await _context.Permissions
                .ToListAsync();

            var currentAssignments = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Select(rp => new PermissionAssignmentDto
                {
                    RoleId = rp.RoleId,
                    ControllerName = rp.Permission!.Name,
                    ActionName = "Access"
                })
                .ToListAsync();

            var response = new PermissionMatrixResponse
            {
                Roles = roles,
                Endpoints = permissions.Select(p => new EndpointMatrixDto
                {
                    Controller = p.Name,
                    Action = "Access",
                    HttpMethod = "AUTH",
                    Route = p.Description
                }).ToList(),
                Permissions = currentAssignments
            };

            return Ok(response);
        }

        [HttpPost("assign")]
        public async Task<ActionResult<ApiResponse<object>>> AssignPermission(PermissionAssignRequest request)
        {
            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == request.ControllerName);
            if (permission == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Permission not found"
                });
            }

            var existing = await _context.RolePermissions.FirstOrDefaultAsync(rp => 
                rp.RoleId == request.RoleId && 
                rp.PermissionId == permission.Id);

            if (request.IsEnabled)
            {
                if (existing == null)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = request.RoleId,
                        PermissionId = permission.Id
                    });
                }
            }
            else
            {
                if (existing != null)
                {
                    _context.RolePermissions.Remove(existing);
                }
            }

            await _context.SaveChangesAsync();

            // 🚀 Update the singleton cache immediately
            var updatedPerms = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == request.RoleId)
                .Select(rp => rp.Permission!.Name)
                .ToListAsync();
            
            _rbacCache.UpdateRolePermissions(request.RoleId, updatedPerms.ToHashSet());

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Permission updated successfully"
            });
        }
    }
}
