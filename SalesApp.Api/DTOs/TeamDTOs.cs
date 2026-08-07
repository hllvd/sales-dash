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
}
