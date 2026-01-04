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
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Contract number must be alphanumeric (hyphens and underscores allowed)")]
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
        
        [Range(1, 100, ErrorMessage = "Contract type must be between 1 and 100")]
        public int? ContractType { get; set; }
        [Range(1, 1000, ErrorMessage = "Quota must be between 1 and 1000")]
        public int? Quota { get; set; }
        [StringLength(200)]
        [ValidUserName]
        [ValidateXSS]
        public string? CustomerName { get; set; }
        
        public int? PvId { get; set; }
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Matricula must be alphanumeric (hyphens and underscores allowed)")]
        public string? MatriculaNumber { get; set; }
    }
}