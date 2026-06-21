namespace SalesApp.DTOs
{
    public class UnresolvedUserInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public List<UserResponse> SuggestedMatches { get; set; } = new();
    }

    public class MatriculaChangeInfo
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string OldMatricula   { get; set; } = string.Empty;
        public string NewMatricula   { get; set; } = string.Empty;
    }

    public class UserContractCountDelta
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Before { get; set; }
        public int After { get; set; }
        public int Delta { get; set; }
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
        public List<string> CreatedPVs { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> DesistenteContractNumbers { get; set; } = new();
        public List<MatriculaChangeInfo> MatriculaChanges { get; set; } = new();
        public List<Dictionary<string, string>> FailedRowsDetails { get; set; } = new();
        public List<UserContractCountDelta> UserContractCountDeltas { get; set; } = new();
    }
}
