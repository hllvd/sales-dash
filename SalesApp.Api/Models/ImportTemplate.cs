using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ImportTemplate
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty; // "Contract", "User", etc.
        
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public string RequiredFields { get; set; } = "[]"; // JSON array
        
        [Required]
        public string OptionalFields { get; set; } = "[]"; // JSON array
        
        [Required]
        public string DefaultMappings { get; set; } = "{}"; // JSON object
        
        public bool IsActive { get; set; } = true;
        
        public Guid CreatedByUserId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public User CreatedBy { get; set; } = null!;
    }
}
