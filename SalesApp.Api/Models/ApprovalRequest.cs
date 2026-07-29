using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class ApprovalRequest
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string RequestType { get; set; } = string.Empty; // "ChangeParentEmail", "RequestMatricula", "AdminRequestMatricula"

        [Required]
        public Guid RequesterId { get; set; }

        public Guid? ApproverId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = ApprovalRequestStatus.Pending;

        [Required]
        public string PayloadJson { get; set; } = "{}";

        [MaxLength(500)]
        public string? ApproverComment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public virtual User? Requester { get; set; }

        [JsonIgnore]
        public virtual User? Approver { get; set; }
    }
}
