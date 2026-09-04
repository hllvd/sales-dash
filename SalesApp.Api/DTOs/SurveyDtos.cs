using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class CreateSurveyDto
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        [Required]
        public string QuestionType { get; set; } = "yesno"; // "yesno" | "singlechoice" | "multichoice"

        public List<string>? Options { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Pelo menos um usuário deve ser selecionado.")]
        public List<Guid> TargetUserIds { get; set; } = new();
    }

    public class SurveySummaryDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalAnswered { get; set; }
        public int TotalPending { get; set; }
        public int TotalExpired { get; set; }
    }

    public class SurveyIndividualResponseDto
    {
        public int AssignmentId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "pending" | "answered" | "expired"
        public string? Answer { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class SurveyResultDto
    {
        public SurveySummaryDto Summary { get; set; } = new();
        public Dictionary<string, int> AggregateCounts { get; set; } = new();
        public List<SurveyIndividualResponseDto> Responses { get; set; } = new();
    }

    public class SurveyAssignmentDto
    {
        public int AssignmentId { get; set; }
        public Guid SurveyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class AnswerSurveyDto
    {
        [Required]
        public int AssignmentId { get; set; }

        [Required]
        public string Answer { get; set; } = string.Empty;
    }

    public class ResendSurveyDto
    {
        public List<int>? AssignmentIds { get; set; }
    }

    public class UserSurveyHistoryDto
    {
        public int AssignmentId { get; set; }
        public Guid SurveyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Answer { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
    }
}
