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
        public string? ParentEmail { get; set; }

        public string? Matricula { get; set; }

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

    public class MergeUserPair
    {
        public string MainEmail { get; set; } = string.Empty;
        public string DuplicateEmail { get; set; } = string.Empty;
    }

    public class MergeUsersRequest
    {
        public List<MergeUserPair> Pairs { get; set; } = new List<MergeUserPair>();
        public bool DeactivateDuplicate { get; set; } = false;
        public bool DryRun { get; set; } = true;
    }

    public class MergeUserPairResult
    {
        public string MainEmail { get; set; } = string.Empty;
        public string DuplicateEmail { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int ContractsMigrated { get; set; }
        public int MatriculasMigrated { get; set; }
        public int ChildUsersMigrated { get; set; }
        public int TeamMembershipsMigrated { get; set; }
        public bool DuplicateDeactivated { get; set; }
    }

    public class MergeUsersResult
    {
        public bool IsDryRun { get; set; }
        public List<MergeUserPairResult> Pairs { get; set; } = new List<MergeUserPairResult>();
    }

    public class MergeMatriculaPair
    {
        public string MainMatricula { get; set; } = string.Empty;
        public string DuplicateMatricula { get; set; } = string.Empty;
    }

    public class MergeMatriculasRequest
    {
        public List<MergeMatriculaPair> Pairs { get; set; } = new List<MergeMatriculaPair>();
        public bool DeleteDuplicate { get; set; } = false;
        public bool DryRun { get; set; } = true;
    }

    public class MergeMatriculaPairResult
    {
        public string MainMatricula { get; set; } = string.Empty;
        public string DuplicateMatricula { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int UserLinksMigrated { get; set; }
        public int ContractsMigrated { get; set; }
        public bool DuplicateDeleted { get; set; }
    }

    public class MergeMatriculasResult
    {
        public bool IsDryRun { get; set; }
        public List<MergeMatriculaPairResult> Pairs { get; set; } = new List<MergeMatriculaPairResult>();
    }
}


