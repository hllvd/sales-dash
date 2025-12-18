namespace SalesApp.DTOs
{
    public class BulkCreateMatriculaResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<UserMatriculaResponse> CreatedMatriculas { get; set; } = new();
        public List<BulkImportError> Errors { get; set; } = new();
    }

    public class BulkImportError
    {
        public int RowNumber { get; set; }
        public string MatriculaNumber { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
