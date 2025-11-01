namespace SalesApp.Models
{
    public static class UserRole
    {
        public const string User = "user";
        public const string Admin = "admin";
        public const string SuperAdmin = "superadmin";
        
        public static readonly string[] ValidRoles = { User, Admin, SuperAdmin };
        
        public static bool IsValid(string role)
        {
            return ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }
    }
}