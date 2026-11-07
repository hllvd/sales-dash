namespace SalesApp.DTOs
{
    public class GroupImportResponse
    {
        public int TotalRows { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ErrorCount => Errors.Count;
        public List<GroupImportRowError> Errors { get; set; } = new();
    }

    public class GroupImportRowError
    {
        public int RowNumber { get; set; }
        public string? GroupName { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
