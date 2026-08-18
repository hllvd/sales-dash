namespace SalesApp.Models
{
    public class ScrapeResult
    {
        public string JobId { get; set; } = string.Empty;
        public string? RunId { get; set; }
        public string? UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Store { get; set; }
        public string? Matricula { get; set; }
        public int RowCount { get; set; }
        public string? FileRelativePath { get; set; }
        public string? Error { get; set; }

        public string? AuthStatus { get; set; }
        public string? AuthMessage { get; set; }
        public bool PowerBiLoaded { get; set; }
        public List<string>? AuthSteps { get; set; }
        public int RetryCount { get; set; }
        public string? ScrapeDate { get; set; }
        public string? DetectedStore { get; set; }
    }
}
