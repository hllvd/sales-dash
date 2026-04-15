namespace SalesApp.Models
{
    public class ScrapeResult
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Store { get; set; }
        public string? Matricula { get; set; }
        public int RowCount { get; set; }
        public string? FileRelativePath { get; set; }
        public string? Error { get; set; }
    }
}
