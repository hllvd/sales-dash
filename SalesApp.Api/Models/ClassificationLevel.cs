using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class ClassificationLevel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Prize { get; set; }

        public decimal? SalesGoal { get; set; }

        /// <summary>Retention target as a percentage (0–100).</summary>
        public int? Retention { get; set; }

        // ── Chain: next tier up ──────────────────────────────────────────────
        public int? NextLevelId { get; set; }
        public virtual ClassificationLevel? NextLevel { get; set; }

        // ── Minimum Direct Rule #1 ───────────────────────────────────────────
        /// <summary>FK to the classification that must be directly below (rule 1).</summary>
        public int? MinimumDirect1LevelId { get; set; }
        public virtual ClassificationLevel? MinimumDirect1Level { get; set; }

        /// <summary>Minimum number of people in MinimumDirect1Level directly below; 0 = unlimited.</summary>
        public int? MinimumDirect1MinCount { get; set; }

        // ── Minimum Direct Rule #2 ───────────────────────────────────────────
        /// <summary>FK to the classification that must be directly below (rule 2).</summary>
        public int? MinimumDirect2LevelId { get; set; }
        public virtual ClassificationLevel? MinimumDirect2Level { get; set; }

        /// <summary>Minimum number of people in MinimumDirect2Level directly below; 0 = unlimited.</summary>
        public int? MinimumDirect2MinCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<UserClassification> UserClassifications { get; set; } = new List<UserClassification>();
    }
}
