using System;

namespace SalesApp.Helpdesk.Models
{
    public class KnowledgeBaseArticle
    {
        public string ArticleId { get; set; } = string.Empty;
        public string QuestionId { get; set; } = string.Empty;
        public string Category { get; set; } = "FAQ"; // "FAQ" or "COMO_FAZER"
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string SummaryContent { get; set; } = string.Empty;
        public string Status { get; set; } = "published"; // "draft" or "published"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
