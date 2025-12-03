using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ContractRequest
    {
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public int GroupId { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "active";
        
        public DateTime ContractStartDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? ContractEndDate { get; set; }
        // New nullable fields
        public int? ContractType { get; set; }
        
        public int? Quota { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }
    }
}