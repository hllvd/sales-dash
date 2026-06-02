using System;
using System.Collections.Generic;
using SalesApp.ReportViews.Models;

namespace SalesApp.ReportViews.DTOs
{
    public class ReportViewResponse
    {
        public string ViewId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Scope { get; set; } = "private";
        public List<ViewRow> Rows { get; set; } = new();
        public List<int>? AllowedTeamIds { get; set; }
        public List<string>? AllowedRoles { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
