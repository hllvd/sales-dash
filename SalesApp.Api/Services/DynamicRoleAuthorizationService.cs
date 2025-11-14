using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class DynamicRoleAuthorizationService : IDynamicRoleAuthorizationService
    {
        private readonly IRoleRepository _roleRepository;

        public DynamicRoleAuthorizationService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<bool> HasPermissionAsync(string userRoleName, string[] requiredRoles)
        {
            var role = await _roleRepository.GetByNameAsync(userRoleName);
            
            if (role == null || !role.IsActive)
                return false;

            return requiredRoles.Contains(role.Name);
        }
    }
}