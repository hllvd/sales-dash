namespace SalesApp.Models
{
    /// <summary>
    /// Role ID enumeration matching database role IDs
    /// </summary>
    public enum RoleId
    {
        /// <summary>
        /// SuperAdmin role - full system access
        /// </summary>
        SuperAdmin = 1,
        
        /// <summary>
        /// Admin role - administrative access
        /// </summary>
        Admin = 2,
        
        /// <summary>
        /// User role - standard user access
        /// </summary>
        User = 3
    }

    /// <summary>
    /// Extension methods for RoleId enum
    /// </summary>
    public static class RoleIdExtensions
    {
        /// <summary>
        /// Converts role name string to RoleId enum
        /// </summary>
        public static RoleId FromRoleName(string roleName)
        {
            return roleName.ToLowerInvariant() switch
            {
                "superadmin" => RoleId.SuperAdmin,
                "admin" => RoleId.Admin,
                "user" => RoleId.User,
                _ => throw new ArgumentException($"Invalid role name: {roleName}")
            };
        }

        /// <summary>
        /// Converts RoleId enum to role name string
        /// </summary>
        public static string ToRoleName(this RoleId roleId)
        {
            return roleId switch
            {
                RoleId.SuperAdmin => "superadmin",
                RoleId.Admin => "admin",
                RoleId.User => "user",
                _ => throw new ArgumentException($"Invalid role ID: {roleId}")
            };
        }
    }
}
