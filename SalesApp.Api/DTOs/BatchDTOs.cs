using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class BatchUpdateParentRequest
    {
        public string ParentEmail { get; set; } = string.Empty;

        public bool OverrideExisting { get; set; }

        public int? TeamId { get; set; }

        public string? Matricula { get; set; }
    }

    public class BatchUpdateParentResult
    {
        public List<ModifiedUserSummary> Modified { get; set; } = new List<ModifiedUserSummary>();
        public List<SkippedUserSummary> Skipped { get; set; } = new List<SkippedUserSummary>();
    }

    public class ModifiedUserSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? OldParentEmail { get; set; }
        public string NewParentEmail { get; set; } = string.Empty;
    }

    public class SkippedUserSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? CurrentParentEmail { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BatchAssignTeamRequest
    {
        public string ParentEmail { get; set; } = string.Empty;

        public int TeamId { get; set; }

        public DateTime? StartDate { get; set; }

        public bool OverrideExisting { get; set; }
    }

    public class BatchAssignTeamResult
    {
        public List<AddedMemberSummary> Added { get; set; } = new List<AddedMemberSummary>();
        public List<SkippedUserSummary> Skipped { get; set; } = new List<SkippedUserSummary>();
    }

    public class AddedMemberSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
