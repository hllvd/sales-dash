namespace SalesApp.ReportFilters.DTOs
{
    /// <summary>
    /// Request body for PUT /api/report-filters/{filterId}.
    /// Replaces filters and columns of an existing report.
    /// </summary>
    public class UpdateReportFilterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Scope { get; set; } = string.Empty;
        public FilterConfigRequest FilterConfig { get; set; } = new();
        public List<OutputColumnRequest> OutputColumns { get; set; } = new();
        public bool GroupByEmail { get; set; }
    }
}
