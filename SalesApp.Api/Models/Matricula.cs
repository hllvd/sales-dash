using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class Matricula
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string MatriculaNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int? ImportSessionId { get; set; }
        
        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<UserMatricula> UserMatriculas { get; set; } = new List<UserMatricula>();
        [JsonIgnore]
        public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
        [JsonIgnore]
        public ImportSession? ImportSession { get; set; }
    }
}
