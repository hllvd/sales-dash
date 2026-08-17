using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesApp.Models
{
    public class ScrapeConfig
    {
        [Key]
        public int Id { get; set; }

        private Guid? _userId;
        public Guid? UserId
        {
            get => User?.Id ?? _userId;
            set => _userId = value;
        }

        public int? UserInternalId { get; set; }

        public virtual User? User { get; set; }

        [Required]
        [MaxLength(200)]
        public string Store { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Matricula { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? PowerBiPassword { get; set; } // Stored encrypted

        [MaxLength(50)]
        public string? CredentialStatus { get; set; } // "ok", "wrong-password", or null

        [MaxLength(20)]
        public string? DefaultStartMonth { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
