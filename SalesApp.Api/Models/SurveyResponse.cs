using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class SurveyResponse
    {
        public int Id { get; set; }

        [Required]
        public int SurveyAssignmentId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Answer { get; set; } = string.Empty;

        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [JsonIgnore]
        public virtual SurveyAssignment? Assignment { get; set; }

        [JsonIgnore]
        public virtual User? User { get; set; }
    }
}
