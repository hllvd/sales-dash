using System;
using System.Collections.Generic;

namespace SalesApp.ReportViews.Models
{
    /// <summary>
    /// Represents a single column slot in a view row, linking to a saved report filter.
    /// </summary>
    public class ViewColumn
    {
        public string? ReportFilterId { get; set; }
    }

    /// <summary>
    /// Represents a row in the view layout, containing 1, 2, or 3 columns.
    /// </summary>
    public class ViewRow
    {
        public List<ViewColumn> Columns { get; set; } = new();
    }

    /// <summary>
    /// Represents a saved dashboard view containing basic details, layout, and permissions.
    /// </summary>
    public class ReportView
    {
        public string PK { get; set; } = "VIEW#";
        public string SK { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Scope { get; set; } = "private";
        public List<ViewRow> Rows { get; set; } = new();
        public List<int>? AllowedTeamIds { get; set; }
        public List<string>? AllowedRoles { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
