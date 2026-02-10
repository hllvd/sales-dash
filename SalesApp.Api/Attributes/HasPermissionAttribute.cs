using Microsoft.AspNetCore.Authorization;

namespace SalesApp.Attributes
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission) : base(permission)
        {
        }
    }
}
