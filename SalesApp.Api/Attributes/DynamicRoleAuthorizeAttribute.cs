using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SalesApp.Services;
using System.Security.Claims;

namespace SalesApp.Attributes
{
    public class DynamicRoleAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _requiredRoles;

        public DynamicRoleAuthorizeAttribute(params string[] requiredRoles)
        {
            _requiredRoles = requiredRoles;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole))
            {
                context.Result = new ForbidResult();
                return;
            }

            var authService = context.HttpContext.RequestServices
                .GetRequiredService<IDynamicRoleAuthorizationService>();

            var hasPermission = await authService.HasPermissionAsync(userRole, _requiredRoles);
            
            if (!hasPermission)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}