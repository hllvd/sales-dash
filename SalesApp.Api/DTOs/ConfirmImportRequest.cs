namespace SalesApp.DTOs
{
    public class ConfirmImportRequest
    {
        // Date format: "MM/DD/YYYY" or "DD/MM/YYYY"
        public string DateFormat { get; set; } = "MM/DD/YYYY";
        public bool SkipMissingContractNumber { get; set; } = false;
        public bool AllowAutoCreateGroups { get; set; } = false;
        public bool AllowAutoCreatePVs { get; set; } = false;
    }
}
