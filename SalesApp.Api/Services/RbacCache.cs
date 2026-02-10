using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SalesApp.Services
{
    public class RbacCache : IRbacCache
    {
        private readonly ConcurrentDictionary<int, HashSet<string>> _rolePermissions = new();

        public void Initialize(Dictionary<int, HashSet<string>> rolePermissions)
        {
            _rolePermissions.Clear();
            foreach (var kvp in rolePermissions)
            {
                _rolePermissions[kvp.Key] = kvp.Value;
            }
        }

        public void UpdateRolePermissions(int roleId, HashSet<string> permissions)
        {
            _rolePermissions[roleId] = permissions;
        }

        public bool RoleHasPermission(int roleId, string permission)
        {
            if (_rolePermissions.TryGetValue(roleId, out var permissions))
            {
                return permissions.Contains(permission) || permissions.Contains("system:superadmin");
            }
            return false;
        }

        public HashSet<string> GetRolePermissions(int roleId)
        {
            if (_rolePermissions.TryGetValue(roleId, out var permissions))
            {
                return permissions;
            }
            return new HashSet<string>();
        }
    }
}
