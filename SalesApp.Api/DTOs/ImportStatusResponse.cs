namespace SalesApp.DTOs
{
    public class UnresolvedUserInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public List<UserResponse> SuggestedMatches { get; set; } = new();
    }
    
    public class ImportStatusResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int FailedRows { get; set; }
        public List<UnresolvedUserInfo> UnresolvedUsers { get; set; } = new();
        public List<string> CreatedGroups { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
