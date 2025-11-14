using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class RoleRequest
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;
        
        [Range(1, 10)]
        public int Level { get; set; } = 1;
        
        public string? Permissions { get; set; }
    }
    
    public class UpdateRoleRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }
        
        [MaxLength(200)]
        public string? Description { get; set; }
        
        [Range(1, 10)]
        public int? Level { get; set; }
        
        public string? Permissions { get; set; }
        
        public bool? IsActive { get; set; }
    }
}