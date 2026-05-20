using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        private Guid _userId;
        [Required]
        public Guid UserId
        {
            get => User?.Id ?? _userId;
            set => _userId = value;
        }

        [Required]
        public int UserInternalId { get; set; } // Who made the change (internal surrogate key)

        [Required]
        [MaxLength(20)]
        public string Action { get; set; } = string.Empty; // "Create", "Update", "Delete"

        [Required]
        [MaxLength(50)]
        public string EntityName { get; set; } = string.Empty; // e.g., "Contract", "User"

        [Required]
        [MaxLength(100)]
        public string EntityId { get; set; } = string.Empty; // The ID of the record changed

        public string? Changes { get; set; } // JSON blob of changed fields: { "Field": { "old": "x", "new": "y" } }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}
