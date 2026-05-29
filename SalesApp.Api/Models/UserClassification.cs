using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class UserClassification
    {
        public int Id { get; set; }

        [Required]
        public int UserInternalId { get; set; }

        [Required]
        public int LevelId { get; set; }

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public virtual User User { get; set; } = null!;

        public virtual ClassificationLevel Level { get; set; } = null!;
    }
}
