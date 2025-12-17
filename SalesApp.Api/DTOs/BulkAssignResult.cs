namespace SalesApp.DTOs
{
    public class BulkAssignResult
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<UserMatriculaResponse> Created { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
