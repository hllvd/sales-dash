using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ImportSession
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string UploadId { get; set; } = string.Empty; // Timestamp-based unique ID
        
        public int? TemplateId { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string FileType { get; set; } = string.Empty; // "csv" or "xlsx"
        
        [Required]
        public Guid UploadedByUserId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "preview"; // "preview", "mapping", "completed", "failed"
        
        public int TotalRows { get; set; }
        
        public int ProcessedRows { get; set; }
        
        public int FailedRows { get; set; }
        
        public string? FileData { get; set; } // JSON array of row data
        
        public string? Mappings { get; set; } // JSON object of column mappings
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        // Navigation properties
        public ImportTemplate? Template { get; set; }
        public User UploadedBy { get; set; } = null!;
    }
}
