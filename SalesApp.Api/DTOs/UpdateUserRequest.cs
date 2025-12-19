using System.ComponentModel.DataAnnotations;
using SalesApp.Models;

namespace SalesApp.DTOs
{
    public class UpdateUserRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }
        
        [EmailAddress]
        public string? Email { get; set; }
        
        [MinLength(6)]
        public string? Password { get; set; }
        
        [MaxLength(20)]
        public string? Role { get; set; }
        
        public Guid? ParentUserId { get; set; }
        
        [EmailAddress]
        public string? ParentUserEmail { get; set; }
        
        public bool? IsActive { get; set; }
    }
}