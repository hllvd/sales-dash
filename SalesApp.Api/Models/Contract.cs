using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class Contract
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;
        
        public Guid? UserId { get; set; }
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        
        public int? GroupId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active"; // "active", "delinquent", "paid_off"
        
        public DateTime SaleStartDate { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(50)]
        public string? UploadId { get; set; } // Tracks which import session created this contract
        
        public int? PvId { get; set; } // Optional link to PV (Ponto de Venda)

        [MaxLength(200)]
        public string? CustomerName { get; set; } // New field
        
        public int? ContractType { get; set; }
        
        public int? Quota { get; set; }
        
        public int? MatriculaId { get; set; } // Link to UserMatricula
        
        // Navigation properties
        public User? User { get; set; }
        public Group? Group { get; set; }
        public PV? PV { get; set; }
        public UserMatricula? UserMatricula { get; set; }
    }
}