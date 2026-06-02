using SalesApp.DTOs;

namespace SalesApp.ReportFilters.DTOs
{
    /// <summary>
    /// Paginated results of executing a saved report against the Contracts table.
    /// Fields are projected down to only those listed in the report's outputColumns.
    /// </summary>
    public class ReportResultsResponse
    {
        /// <summary>Page number (1-based).</summary>
        public int Page { get; set; }

        /// <summary>Number of items per page.</summary>
        public int PageSize { get; set; }

        /// <summary>Total number of matching contracts (before pagination).</summary>
        public int TotalCount { get; set; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Projected rows — each row is a dictionary of { columnLabel → value }
        /// keyed by OutputColumn.Label so the client can use it as column headers.
        /// </summary>
        public List<Dictionary<string, object?>> Rows { get; set; } = new();

        /// <summary>Ordered column definitions used for this result set.</summary>
        public List<OutputColumnResponse> Columns { get; set; } = new();

        public decimal? TotalSum { get; set; }
    }
}
