using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class UserMatricula
    {
        public int Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string MatriculaNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active"; // See MatriculaStatus enum for valid values
        
        public bool IsOwner { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int? ImportSessionId { get; set; } // Tracks if this matricula was created via import
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public ImportSession? ImportSession { get; set; }
    }
}
