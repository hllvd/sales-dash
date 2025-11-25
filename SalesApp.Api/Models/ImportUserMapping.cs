using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ImportUserMapping
    {
        public int Id { get; set; }
        
        [Required]
        public int ImportSessionId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string SourceName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string SourceSurname { get; set; } = string.Empty;
        
        public Guid? ResolvedUserId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Action { get; set; } = "pending"; // "mapped", "created", "pending"
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ImportSession ImportSession { get; set; } = null!;
        public User? ResolvedUser { get; set; }
    }
}
