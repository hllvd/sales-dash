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
        public bool GroupByTeam { get; set; }
        public bool GroupByClassification { get; set; }
        public bool HideUnassignedTeams { get; set; }
        public string? OrderByField { get; set; }
        public string? OrderByDirection { get; set; }
        public List<int>? AllowedTeamIds { get; set; }
        public List<string>? AllowedRoles { get; set; }
        public bool SumTotal { get; set; }
        public string OutputType { get; set; } = "table";
        public string ChartType { get; set; } = "bar";
        public string? SummaryRetentionType { get; set; } = "standard";
        public string? ChartMetric { get; set; }
    }
}
