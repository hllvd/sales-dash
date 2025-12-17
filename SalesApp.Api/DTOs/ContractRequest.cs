using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ContractRequest
    {
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;
        
        public Guid? UserId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }
        
        
        public int? GroupId { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "Active";
        
        public DateTime ContractStartDate { get; set; } = DateTime.UtcNow;
        
        // New nullable fields
        public int? ContractType { get; set; }
        
        public int? Quota { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }
        
        public int? PvId { get; set; }
        
        [MaxLength(50)]
        public string? MatriculaNumber { get; set; }
    }
}