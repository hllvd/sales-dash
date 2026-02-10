using System.Collections.Generic;

namespace SalesApp.Services
{
    public interface IRbacCache
    {
        void Initialize(Dictionary<int, HashSet<string>> rolePermissions);
        void UpdateRolePermissions(int roleId, HashSet<string> permissions);
        bool RoleHasPermission(int roleId, string permission);
        HashSet<string> GetRolePermissions(int roleId);
    }
}
