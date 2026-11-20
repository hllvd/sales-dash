using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class CreateApprovalRequestDto
    {
        [Required]
        [MaxLength(50)]
        public string RequestType { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = "{}";
    }

    public class ResolveApprovalDto
    {
        [Required]
        public string Action { get; set; } = string.Empty; // "Approved", "Rejected", "Later"

        [MaxLength(500)]
        public string? Comment { get; set; }
    }

    public class ApprovalRequestResponse
    {
        public int Id { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public Guid RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string RequesterEmail { get; set; } = string.Empty;
        public Guid? ApproverId { get; set; }
        public string? ApproverName { get; set; }
        public string Status { get; set; } = "Pending";
        public string PayloadJson { get; set; } = "{}";
        public string? ApproverComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ChangeParentEmailPayload
    {
        public string NewParentEmail { get; set; } = string.Empty;
    }

    public class RequestMatriculaPayload
    {
        public string MatriculaNumber { get; set; } = string.Empty;
    }

    public class AdminRequestMatriculaPayload
    {
        public string MatriculaNumber { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
    }

    public class RequestClassificationLevelPayload
    {
        public int LevelId { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public int? PreviousLevelId { get; set; }
        public string? PreviousLevelName { get; set; }
        public DateTime? PreviousEndDate { get; set; }
    }

    public class CreateTeamPayload
    {
        public string TeamName { get; set; } = string.Empty;
        public string? NewEmail { get; set; }
    }

    public class RequestContractTransferPayload
    {
        public string ContractNumber { get; set; } = string.Empty;
        public int? ContractId { get; set; }
        public Guid? CurrentUserId { get; set; }
        public string? CurrentUserName { get; set; }
    }

    public class ResolveBatchApprovalDto
    {
        public List<int> RequestIds { get; set; } = new();
        public string Action { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }

    public class ResolveBatchApprovalResult
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<int> SucceededIds { get; set; } = new();
        public List<BatchFailureItem> Failures { get; set; } = new();
    }

    public class BatchFailureItem
    {
        public int RequestId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
