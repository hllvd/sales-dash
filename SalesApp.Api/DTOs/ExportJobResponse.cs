namespace SalesApp.DTOs
{
    public class ExportJobResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending | processing | completed | failed
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
