using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class UpdateContractRequest
    {
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [ValidAlphanumeric(required: false)]
        public string? ContractNumber { get; set; }
        
        public Guid? UserId { get; set; }
        
        [Range(0.01, double.MaxValue)]
        public decimal? TotalAmount { get; set; }
        
        public int? GroupId { get; set; }
        
        [StringLength(20)]
        [ValidContractStatus]
        public string? Status { get; set; }
        
        public DateTime? ContractStartDate { get; set; }
        
        public bool? IsActive { get; set; }
        
        [StringLength(20)]
        public string? ContractType { get; set; }
        
        public int? Quota { get; set; }
        
        public int? PvId { get; set; }
        public string? MatriculaNumber { get; set; }

        [StringLength(200)]
        [ValidUserName]
        [ValidateXSS]
        public string? CustomerName { get; set; }
        public int? UserMatriculaId { get; set; }
    }
}