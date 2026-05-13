namespace SalesApp.ReportFilters.Models
{
    /// <summary>
    /// Domain model for a saved report filter configuration.
    /// Not exposed directly from the API — use ReportFilterResponse DTO instead.
    /// </summary>
    public class ReportFilter
    {
        /// <summary>DynamoDB partition key — always "REP#".</summary>
        public string PK { get; set; } = "REP#";

        /// <summary>DynamoDB sort key — "#U-{userGuid}#REP-{filterId}".</summary>
        public string SK { get; set; } = string.Empty;

        /// <summary>Plain user GUID of the owner.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>ULID-based unique report identifier.</summary>
        public string FilterId { get; set; } = string.Empty;

        /// <summary>Human-readable name (max 100 chars).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description (max 500 chars).</summary>
        public string? Description { get; set; }

        /// <summary>"private" or "shared".</summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>Filter criteria.</summary>
        public FilterConfig FilterConfig { get; set; } = new();

        /// <summary>Ordered list of output columns.</summary>
        public List<OutputColumn> OutputColumns { get; set; } = new();

        /// <summary>When true, groups results by owner email and sums totalAmount.</summary>
        public bool GroupByEmail { get; set; }

        /// <summary>Column label to order by.</summary>
        public string? OrderByField { get; set; }

        /// <summary>"asc" or "desc".</summary>
        public string? OrderByDirection { get; set; }

        /// <summary>UTC ISO 8601 creation timestamp.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC ISO 8601 last-update timestamp.</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
