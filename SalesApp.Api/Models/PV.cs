using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesApp.Models
{
    public class PV
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // NOT auto-increment
        public int Id { get; set; }
        
        [Required]
        [Column(TypeName = "text")]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Optional foreign key to UserMatricula
        public int? MatriculaId { get; set; }
        
        // Navigation property
        public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}
