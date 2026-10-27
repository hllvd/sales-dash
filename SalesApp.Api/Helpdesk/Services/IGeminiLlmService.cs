using System.Threading.Tasks;
using SalesApp.Helpdesk.Models;

namespace SalesApp.Helpdesk.Services
{
    public class GeminiSummaryResult
    {
        public string Category { get; set; } = "FAQ"; // "FAQ" or "COMO_FAZER"
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public interface IGeminiLlmService
    {
        Task<GeminiSummaryResult> GenerateSummaryAsync(HelpdeskQuestion question);
    }
}
