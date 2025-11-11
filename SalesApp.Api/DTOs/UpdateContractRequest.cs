using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class UpdateContractRequest
    {
        public Guid? UserId { get; set; }
        
        [Range(0.01, double.MaxValue)]
        public decimal? TotalAmount { get; set; }
        
        public Guid? GroupId { get; set; }
        
        [MaxLength(20)]
        public string? Status { get; set; }
        
        public DateTime? ContractStartDate { get; set; }
        
        public DateTime? ContractEndDate { get; set; }
        
        public bool? IsActive { get; set; }
    }
}