namespace SalesApp.DTOs
{
    public class ImportPreviewResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int? TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public List<string> DetectedColumns { get; set; } = new();
        public List<Dictionary<string, string>> SampleRows { get; set; } = new();
        public int TotalRows { get; set; }
        public Dictionary<string, string> SuggestedMappings { get; set; } = new();
        public List<string> RequiredFields { get; set; } = new();
        public List<string> OptionalFields { get; set; } = new();
        public bool IsTemplateMatch { get; set; } = true;
        public string? MatchMessage { get; set; }
    }
}
