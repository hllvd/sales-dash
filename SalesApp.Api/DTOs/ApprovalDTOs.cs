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
}
