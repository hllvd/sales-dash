namespace SalesApp.DTOs
{
    public class ImportSessionResponse
    {
        public int Id { get; set; }
        public string UploadId { get; set; } = string.Empty;
        public int? TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateEntityType { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int FailedRows { get; set; }
        public string? Mappings { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? UploadedByName { get; set; }
        public string? UploadedByEmail { get; set; }
    }
}
