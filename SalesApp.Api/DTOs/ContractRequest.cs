using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class ContractRequest
    {
        [Required(ErrorMessage = "O número do contrato é obrigatório.")]
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [ValidAlphanumeric]
        [Display(Name = "Número do Contrato")]
        public string ContractNumber { get; set; } = string.Empty;
        
        [Display(Name = "Vendedor")]
        public Guid? UserId { get; set; }
        
        [Required(ErrorMessage = "O valor total é obrigatório.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "O valor total deve ser de no mínimo 0.01.")]
        [Display(Name = "Valor Total")]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Grupo")]
        public int? GroupId { get; set; }
        
        [StringLength(20)]
        [ValidContractStatus]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";
        
        [Required(ErrorMessage = "A data de início do contrato é obrigatória.")]
        [Display(Name = "Data de Início")]
        public DateTime ContractStartDate { get; set; } = DateTime.UtcNow;
        
        [StringLength(20)]
        [Display(Name = "Tipo de Contrato")]
        public string? ContractType { get; set; }

        [Display(Name = "Cota")]
        public int? Quota { get; set; }

        [StringLength(200)]
        [ValidUserName]
        [ValidateXSS]
        [Display(Name = "Nome do Cliente")]
        public string? CustomerName { get; set; }
        
        public int? PvId { get; set; }
        public string? MatriculaNumber { get; set; }
        public int? UserMatriculaId { get; set; }
    }
}