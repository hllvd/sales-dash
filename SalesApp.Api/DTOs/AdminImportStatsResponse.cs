using System;

namespace SalesApp.DTOs
{
    public class AdminImportStatsResponse
    {
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime? LastImportAt { get; set; }
        public int TotalImports { get; set; }
    }
}
