using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class PendingContractClaim
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;

        private Guid _userId;
        [Required]
        public Guid UserId
        {
            get => User?.Id ?? _userId;
            set => _userId = value;
        }

        [Required]
        public int UserInternalId { get; set; }

        [Required]
        public int MatriculaId { get; set; }

        public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;
        public DateTime? ResolvedAt { get; set; }

        // Navigation
        [JsonIgnore]
        public virtual User User { get; set; } = null!;
        [JsonIgnore]
        public virtual Matricula Matricula { get; set; } = null!;
    }
}
