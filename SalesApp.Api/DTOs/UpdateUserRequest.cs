using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using SalesApp.Models;

namespace SalesApp.DTOs
{
    public class UpdateUserRequest
    {
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters")]
        [ValidUserName]
        public string? Name { get; set; }
        
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }
        
        [StringLength(150, MinimumLength = 12, ErrorMessage = "Password must be between 12 and 150 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
        public string? Password { get; set; }
        
        [MaxLength(20, ErrorMessage = "Role cannot exceed 20 characters")]
        [ValidUserRole]
        public string? Role { get; set; }
        
        public Guid? ParentUserId { get; set; }
        
        public bool? IsActive { get; set; }
    }
}