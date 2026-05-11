namespace SalesApp.ReportFilters.Models
{
    /// <summary>
    /// Represents a single selected output column in a saved report configuration.
    /// </summary>
    public class OutputColumn
    {
        /// <summary>
        /// The source entity the field belongs to.
        /// Valid values: "Contracts", "Users_Contract", "Users_Matricula", "Status", "PV", "Group".
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>The actual field name on the source entity as it exists in the codebase.</summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>User-defined display header shown in the report table.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>1-based position; drives column sort order in the rendered table.</summary>
        public int Order { get; set; }
    }
}
