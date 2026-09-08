using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class SurveyAssignment
    {
        public int Id { get; set; }

        [Required]
        public Guid SurveyId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending"; // "pending" | "answered" | "expired"

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(2);

        // Navigation
        [JsonIgnore]
        public virtual Survey? Survey { get; set; }

        [JsonIgnore]
        public virtual User? User { get; set; }

        [JsonIgnore]
        public virtual SurveyResponse? Response { get; set; }
    }
}
