using Microsoft.AspNetCore.Authorization;
using SalesApp.Services;
using System.Security.Claims;

namespace SalesApp.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRbacCache _rbacCache;

        public PermissionHandler(IHttpContextAccessor httpContextAccessor, IRbacCache rbacCache)
        {
            _httpContextAccessor = httpContextAccessor;
            _rbacCache = rbacCache;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, 
            PermissionRequirement requirement)
        {
            var user = context.User;
            if (user == null || !user.Identity!.IsAuthenticated)
            {
                return Task.CompletedTask;
            }

            // 🚀 Immediate Revocation Check: Use live RbacCache if role_id is present
            var roleIdClaim = user.FindFirst("role_id");
            if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out var roleId))
            {
                if (_rbacCache.RoleHasPermission(roleId, requirement.Permission))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
                
                // If role_id is present, but permission is NOT in cache, we DENY access.
                // This is the "Immediate Revocation" logic.
                return Task.CompletedTask;
            }

            // 🔄 Fallback: Use flattened permissions in JWT claims (Legacy/Transition)
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return Task.CompletedTask;

            if (httpContext.Items["UserPermissionsSet"] is not HashSet<string> permissions)
            {
                permissions = user.FindAll("perm").Select(c => c.Value).ToHashSet();
                httpContext.Items["UserPermissionsSet"] = permissions;
            }

            if (permissions.Contains(requirement.Permission) || permissions.Contains("system:superadmin"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
