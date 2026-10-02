using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class TeamHistoryItemDto
    {
        public int UserTeamId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public bool TeamIsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActivePeriod { get; set; }
    }

    public class InconsistencyDescriptorDto
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "warning"; // warning | error | info
    }

    public class MigrationRecommendationDto
    {
        public int? SuggestedTeamId { get; set; }
        public string? SuggestedTeamName { get; set; }
        public DateTime? SuggestedStartDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool SuggestSetAsOwner { get; set; }
    }

    public class AdminTeamInconsistencyItemDto
    {
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? ParentEmail { get; set; }
        public string? ParentUserName { get; set; }
        public string? ParentTeamName { get; set; }
        public int? CurrentTeamId { get; set; }
        public string? CurrentTeamName { get; set; }
        public int? OwnedTeamId { get; set; }
        public string? OwnedTeamName { get; set; }
        public DateTime? EarliestContractDate { get; set; }
        public int TotalContractsCount { get; set; }
        public List<TeamHistoryItemDto> TeamHistory { get; set; } = new List<TeamHistoryItemDto>();
        public List<InconsistencyDescriptorDto> Inconsistencies { get; set; } = new List<InconsistencyDescriptorDto>();
        public MigrationRecommendationDto? Recommendation { get; set; }
    }

    public class AdminMigrateTeamMemberRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public int TargetTeamId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool SetAsOwner { get; set; }

        public bool CloseConflictingPeriods { get; set; } = true;
    }

    public class AdminMigrateTeamMemberResult
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TargetTeamId { get; set; }
        public string TargetTeamName { get; set; } = string.Empty;
        public int ClosedPeriodsCount { get; set; }
        public bool IsOwner { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
