using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class UserMatricula
    {
        public int Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        public int MatriculaId { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public bool IsOwner { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int? ImportSessionId { get; set; } // Tracks if this matricula was created via import
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Matricula Matricula { get; set; } = null!;
        public ImportSession? ImportSession { get; set; }
    }
}
