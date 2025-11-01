using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class SaleRequest
    {
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public Guid GroupId { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "active";
    }
    
    public class UpdateSaleRequest
    {
        public Guid? UserId { get; set; }
        
        [Range(0.01, double.MaxValue)]
        public decimal? TotalAmount { get; set; }
        
        public Guid? GroupId { get; set; }
        
        [MaxLength(20)]
        public string? Status { get; set; }
        
        public bool? IsActive { get; set; }
    }
}