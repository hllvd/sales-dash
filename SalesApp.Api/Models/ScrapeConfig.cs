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

        [MaxLength(200)]
        public string? Store { get; set; }

        [Required]
        [MaxLength(100)]
        public string Matricula { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? PowerBiPassword { get; set; } // Stored encrypted

        [MaxLength(50)]
        public string? CredentialStatus { get; set; } // "ok", "wrong-password", or null

        [MaxLength(20)]
        public string? DefaultStartMonth { get; set; }

        [MaxLength(50)]
        public string ScrapeType { get; set; } = "geral";

        [MaxLength(20)]
        public string OutputMode { get; set; } = "direct"; // "direct" | "sqs"

        public bool AutoImportSqs { get; set; } = true;

        [MaxLength(20)]
        public string? ScheduleMode { get; set; } // "interval", "daily", "weekly", or null

        public int? ScheduleIntervalHours { get; set; }

        [MaxLength(1000)]
        public string? ScheduleTimes { get; set; } // JSON array of "HH:mm" for daily, or JSON object for weekly

        public int? ScrapeIntervalHours { get; set; } // Deprecated, kept in sync for backward compatibility

        public DateTime? LastTriggeredAt { get; set; }

        // Import / Update Options
        public bool SkipMissingContractNumber { get; set; } = true;
        public bool AllowAutoCreateGroups { get; set; } = true;
        public bool AllowAutoCreatePVs { get; set; } = true;
        public bool UpdateMatriculaOnExisting { get; set; } = false;
        public bool UpdateTotalAmountOnExisting { get; set; } = true;
        public bool UpdateStartDateOnExisting { get; set; } = true;

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
