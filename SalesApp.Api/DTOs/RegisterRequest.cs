using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using SalesApp.Models;

namespace SalesApp.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters")]
        [ValidUserName]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        [StringLength(150, MinimumLength = 12, ErrorMessage = "Password must be between 12 and 150 characters")]
        public string Password { get; set; } = string.Empty;
        
        [MaxLength(20, ErrorMessage = "Role cannot exceed 20 characters")]
        [ValidUserRole]
        public string Role { get; set; } = UserRole.User;
        
        public Guid? ParentUserId { get; set; }
        
        [MaxLength(50, ErrorMessage = "MatriculaNumber cannot exceed 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9]*$", ErrorMessage = "MatriculaNumber can only contain alphanumeric characters")]
        public string? MatriculaNumber { get; set; }
        
        public bool IsMatriculaOwner { get; set; } = false;
    }
}