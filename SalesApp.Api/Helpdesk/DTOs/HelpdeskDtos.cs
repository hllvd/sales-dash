using System;
using System.Collections.Generic;

namespace SalesApp.Helpdesk.DTOs
{
    public class CreateHelpdeskQuestionRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? SourcePageUrl { get; set; }
    }

    public class UpdateHelpdeskQuestionRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    public class AddHelpdeskMessageRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateHelpdeskStatusRequest
    {
        public string Status { get; set; } = string.Empty; // "Não lido", "Lida", "Pendente", "Resolvido"
    }

    public class HelpdeskQuestionResponse
    {
        public string QuestionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CurrentContent { get; set; } = string.Empty;
        public string SourcePageUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<HelpdeskMessageResponse> Messages { get; set; } = new();
        public List<HelpdeskEditHistoryResponse> EditHistory { get; set; } = new();
    }

    public class HelpdeskMessageResponse
    {
        public string MessageId { get; set; } = string.Empty;
        public string SenderUserId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class HelpdeskEditHistoryResponse
    {
        public int Version { get; set; }
        public string PreviousContent { get; set; } = string.Empty;
        public DateTime EditedAt { get; set; }
    }

    public class HelpdeskUnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}
