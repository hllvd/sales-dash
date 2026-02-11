using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class ContractRequest
    {
        [Required]
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [ValidAlphanumeric]
        public string ContractNumber { get; set; } = string.Empty;
        
        public Guid? UserId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }
        
        public int? GroupId { get; set; }
        
        [StringLength(20)]
        [ValidContractStatus]
        public string Status { get; set; } = "Active";
        
        public DateTime ContractStartDate { get; set; } = DateTime.UtcNow;
        
        [StringLength(20)]
        public string? ContractType { get; set; }
        public int? Quota { get; set; }
        [StringLength(200)]
        [ValidUserName]
        [ValidateXSS]
        public string? CustomerName { get; set; }
        
        public int? PvId { get; set; }
        public string? MatriculaNumber { get; set; }
        public int? UserMatriculaId { get; set; }
    }
}