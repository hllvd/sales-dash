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
        public bool? CurrentUserTeam { get; set; }
        public bool? CurrentUserMatricula { get; set; }
        public List<string>? Emails { get; set; }
        public List<int>? Groups { get; set; }
        public List<int>? Teams { get; set; }
        /// <summary>"current" (default) or "historical" — controls how team membership is resolved for filtering.</summary>
        public string? TeamMembershipMode { get; set; }
        public List<int>? Pvs { get; set; }
        public List<string>? Statuses { get; set; }
        public string? StatusOperator { get; set; }
        public List<int>? ClassificationLevelIds { get; set; }
        public decimal? MinRetention { get; set; }
        public decimal? MaxRetention { get; set; }
        public decimal? MinStrictRetention { get; set; }
        public decimal? MaxStrictRetention { get; set; }
        public decimal? MinProduction { get; set; }
        public decimal? MaxProduction { get; set; }
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
