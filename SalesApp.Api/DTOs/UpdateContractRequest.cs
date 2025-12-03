using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class UpdateContractRequest
    {
        [MaxLength(50)]
        public string? ContractNumber { get; set; }
        
        public Guid? UserId { get; set; }
        
        [Range(0.01, double.MaxValue)]
        public decimal? TotalAmount { get; set; }
        
        public int? GroupId { get; set; }
        
        [MaxLength(20)]
        public string? Status { get; set; }
        
        public DateTime? ContractStartDate { get; set; }
        
        public DateTime? ContractEndDate { get; set; }
        
        public bool? IsActive { get; set; }
        
        public int? ContractType { get; set; }
        
        public int? Quota { get; set; }
        

        
        public int? PvId { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }
    }
}