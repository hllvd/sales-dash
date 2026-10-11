using System;
using System.Collections.Generic;

namespace SalesApp.Helpdesk.Models
{
    public class HelpdeskQuestion
    {
        public string QuestionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CurrentContent { get; set; } = string.Empty;
        public string SourcePageUrl { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = "Não lido"; // "Não lido", "Lida", "Pendente", "Resolvido"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<HelpdeskMessage> Messages { get; set; } = new();
        public List<HelpdeskEditHistory> EditHistory { get; set; } = new();
    }

    public class HelpdeskMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public string QuestionId { get; set; } = string.Empty;
        public string SenderUserId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class HelpdeskEditHistory
    {
        public int Version { get; set; }
        public string PreviousContent { get; set; } = string.Empty;
        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    }
}
