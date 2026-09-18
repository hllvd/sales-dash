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
        public int DurationSeconds { get; set; }
        public string? DurationFormatted { get; set; }
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }

        // SQS/S3 mode fields
        public string? OutputMode { get; set; }  // "direct" | "sqs"
        public string? S3Key { get; set; }
        public string? S3Bucket { get; set; }
    }
}
