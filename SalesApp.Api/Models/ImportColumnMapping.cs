using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ImportColumnMapping
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string MappingName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string FileType { get; set; } = string.Empty; // "csv" or "xlsx"
        
        [Required]
        [MaxLength(100)]
        public string SourceColumn { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string TargetField { get; set; } = string.Empty;
        
        public bool IsRequired { get; set; }
        
        public Guid CreatedByUserId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public User CreatedBy { get; set; } = null!;
    }
}
