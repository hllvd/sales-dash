using System.ComponentModel.DataAnnotations;
using SalesApp.Models;

namespace SalesApp.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string Role { get; set; } = UserRole.User;
        
        public Guid? ParentUserId { get; set; }
        
        public string? Matricula { get; set; }
        
        public bool IsMatriculaOwner { get; set; } = false;
    }
}