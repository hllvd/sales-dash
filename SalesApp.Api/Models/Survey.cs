using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class Survey
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string QuestionType { get; set; } = "yesno"; // "yesno" | "singlechoice" | "multichoice"

        public string? OptionsJson { get; set; } // JSON serialized string[] for choices

        public bool IsActive { get; set; } = true;

        [Required]
        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [JsonIgnore]
        public virtual User? CreatedBy { get; set; }

        [JsonIgnore]
        public virtual ICollection<SurveyAssignment> Assignments { get; set; } = new List<SurveyAssignment>();
    }
}
