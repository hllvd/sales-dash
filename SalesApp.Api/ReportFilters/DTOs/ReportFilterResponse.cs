namespace SalesApp.ReportFilters.DTOs
{
    /// <summary>
    /// API response for a single saved report filter.
    /// Never exposes internal DynamoDB keys.
    /// </summary>
    public class ReportFilterResponse
    {
        public string FilterId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Scope { get; set; } = string.Empty;
        public FilterConfigResponse FilterConfig { get; set; } = new();
        public List<OutputColumnResponse> OutputColumns { get; set; } = new();
        public bool GroupByEmail { get; set; }
        public bool GroupByTeam { get; set; }
        public bool GroupByClassification { get; set; }
        public bool HideUnassignedTeams { get; set; }
        public string? OrderByField { get; set; }
        public string? OrderByDirection { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<int>? AllowedTeamIds { get; set; }
        public List<string>? AllowedRoles { get; set; }
        public bool SumTotal { get; set; }
        public string OutputType { get; set; } = "table";
        public string ChartType { get; set; } = "bar";
        public string? SummaryRetentionType { get; set; }
        public string? ChartMetric { get; set; }
        public List<ExportedFieldResponse> ExportedFields { get; set; } = new();
    }

    public class ExportedFieldResponse
    {
        public string FieldType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class FilterConfigResponse
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
        public List<int>? Stores { get; set; }
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

    public class OutputColumnResponse
    {
        public string Source { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Order { get; set; }
        public string? Format { get; set; }
    }
}
