using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class TeamMemberRequest
    {
        [Required]
        public Guid UserId { get; set; }

        public DateTime? StartDate { get; set; }
    }

    public class CreateTeamRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public int? StoreId { get; set; }

        public List<TeamMemberRequest> Members { get; set; } = new List<TeamMemberRequest>();
    }

    public class UpdateTeamRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public Guid? OwnerUserId { get; set; }

        public int? StoreId { get; set; }

        public bool ClearStore { get; set; }
    }

    public class TeamMemberResponse
    {
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsOwner { get; set; }
    }

    public class TeamResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? StoreId { get; set; }
        public string? StoreName { get; set; }
        public string? StoreState { get; set; }
        public TeamMemberResponse? Owner { get; set; }
        public List<TeamMemberResponse> Members { get; set; } = new List<TeamMemberResponse>();
        public List<string> Warnings { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AddMembersRequest
    {
        [Required]
        public List<TeamMemberRequest> Members { get; set; } = new List<TeamMemberRequest>();
    }

    public class UpdateMemberDatesRequest
    {
        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }

    public class TeamCalendarUserHistoryItem
    {
        public int UserTeamId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class TeamCalendarUserResponse
    {
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? CurrentTeamName { get; set; }
        public int? CurrentTeamId { get; set; }
        public int HierarchyLevel { get; set; }
        public string? ParentUserName { get; set; }
        public DateTime? EarliestContractDate { get; set; }
        public List<TeamCalendarUserHistoryItem> TeamHistory { get; set; } = new List<TeamCalendarUserHistoryItem>();
    }

    public class CalendarContractPreviewItem
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime SaleStartDate { get; set; }
        public string? CustomerName { get; set; }
        public string? MatriculaNumber { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CalendarContractPreviewResponse
    {
        public List<CalendarContractPreviewItem> OlderTeamContracts { get; set; } = new List<CalendarContractPreviewItem>();
        public List<CalendarContractPreviewItem> NewerTeamContracts { get; set; } = new List<CalendarContractPreviewItem>();
    }

    public class AdjustTeamBoundaryRequest
    {
        [Required]
        public Guid UserId { get; set; }
        public int? OlderTeamId { get; set; }
        public int? NewerTeamId { get; set; }
        [Required]
        public DateTime BoundaryDate { get; set; }
    }

    public class AvailableTeamItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? StoreName { get; set; }
        public string? OwnerName { get; set; }
        public Guid? OwnerUserId { get; set; }
        public int MemberCount { get; set; }
    }

    public class AssignUserTeamRequest
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public int NewTeamId { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        public bool UpdateParentUser { get; set; } = true;
    }
}
