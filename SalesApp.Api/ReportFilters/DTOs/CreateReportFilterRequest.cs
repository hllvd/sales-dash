namespace SalesApp.ReportFilters.DTOs
{
    /// <summary>
    /// Filter config section of a create/update request.
    /// All fields are optional, but at least one must be non-null.
    /// </summary>
    public class FilterConfigRequest
    {
        public List<string>? Matriculas { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RelativeStartDate { get; set; }
        public string? RelativeEndDate { get; set; }
        public bool? CurrentUserAsParent { get; set; }
        public List<string>? Emails { get; set; }
        public List<int>? Groups { get; set; }
        public List<int>? Pvs { get; set; }
        public List<string>? Statuses { get; set; }
        public string? StatusOperator { get; set; }
    }

    /// <summary>
    /// Output column definition inside a create/update request.
    /// </summary>
    public class OutputColumnRequest
    {
        public string Source { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Order { get; set; }
        public string? Format { get; set; }
    }

    /// <summary>
    /// Request body for POST /api/report-filters.
    /// </summary>
    public class CreateReportFilterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Scope { get; set; } = string.Empty;
        public FilterConfigRequest FilterConfig { get; set; } = new();
        public List<OutputColumnRequest> OutputColumns { get; set; } = new();
        public bool GroupByEmail { get; set; }
        public bool GroupByTeam { get; set; }
        public string? OrderByField { get; set; }
        public string? OrderByDirection { get; set; }
    }
}
