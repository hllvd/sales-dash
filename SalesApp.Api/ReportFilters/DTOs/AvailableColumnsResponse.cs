namespace SalesApp.ReportFilters.DTOs
{
    /// <summary>
    /// Response from GET /api/report-filters/columns/available.
    /// Returns the full list of selectable columns grouped by source entity.
    /// Field names are the actual property names as they exist in the domain models.
    /// </summary>
    public class AvailableColumnsResponse
    {
        public List<SourceColumns> Sources { get; set; } = new();
    }

    public class SourceColumns
    {
        public string Source { get; set; } = string.Empty;
        public List<string> Fields { get; set; } = new();
    }
}
