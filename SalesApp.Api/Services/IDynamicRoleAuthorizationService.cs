namespace SalesApp.Services
{
    public interface IDynamicRoleAuthorizationService
    {
        Task<bool> HasPermissionAsync(string userRoleName, string[] requiredRoles);
    }
}