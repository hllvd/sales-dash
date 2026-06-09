namespace SalesApp.ReportFilters.Models
{
    /// <summary>
    /// Typed representation of the filter configuration stored in a saved report.
    /// All fields are optional; at least one must be present on creation.
    /// </summary>
    public class FilterConfig
    {
        /// <summary>Matricula identifier strings (e.g. "MAT-001").</summary>
        public List<string>? Matriculas { get; set; }

        /// <summary>ISO 8601 UTC start of the date range filter applied to SaleStartDate.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>ISO 8601 UTC end of the date range filter applied to SaleStartDate.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Relative start date expression (e.g. "-1M", "-7d").</summary>
        public string? RelativeStartDate { get; set; }

        /// <summary>Relative end date expression (e.g. "-1M", "-7d").</summary>
        public string? RelativeEndDate { get; set; }

        /// <summary>
        /// When true, filters contracts where the currently authenticated user (at query time)
        /// is the parent of the contract owner. Resolved at query time, NOT at save time.
        /// </summary>
        public bool? CurrentUserAsParent { get; set; }

        /// <summary>
        /// When true, injects the authenticated user's currently active team IDs into the Teams
        /// filter at query time. Resolved from the JWT — never stored as a concrete team ID.
        /// </summary>
        public bool? CurrentUserTeam { get; set; }

        /// <summary>
        /// When true, injects the authenticated user's active matricula number(s) into the
        /// Matriculas filter at query time. Resolved from the JWT — never stored as a literal number.
        /// </summary>
        public bool? CurrentUserMatricula { get; set; }

        /// <summary>User email addresses to filter by (all users available).</summary>
        public List<string>? Emails { get; set; }

        /// <summary>Group identifiers to filter by.</summary>
        public List<int>? Groups { get; set; }

        /// <summary>Team identifiers to filter by.</summary>
        public List<int>? Teams { get; set; }

        /// <summary>PV identifiers (integer IDs) to filter by.</summary>
        public List<int>? Pvs { get; set; }

        /// <summary>Contract status names to filter by (e.g. "Active", "Late1").</summary>
        public List<string>? Statuses { get; set; }

        /// <summary>"or" | "and" — logical operator applied to Statuses list. Defaults to "or".</summary>
        public string? StatusOperator { get; set; }
    }
}
