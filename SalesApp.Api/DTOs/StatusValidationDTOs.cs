namespace SalesApp.DTOs
{
    public class ValidateStatusRequest
    {
        public string ColumnName { get; set; } = "";
    }

    public class StatusValidationResponse
    {
        public bool IsValid { get; set; }
        public List<string> InvalidValues { get; set; } = new();
        public List<string> SampleValues { get; set; } = new();
        public int ValidCount { get; set; }
        public int TotalChecked { get; set; }
    }
}
