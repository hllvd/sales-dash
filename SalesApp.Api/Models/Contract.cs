using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalesApp.Models
{
    public class Contract
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;
        
        public int? UserInternalId { get; set; }
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        
        public int? GroupId { get; set; }
        
        [Required]
        public int ContractStatusId { get; set; }

        [JsonIgnore]
        public ContractStatusEntity ContractStatus { get; set; } = null!;
        
        public DateTime SaleStartDate { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(50)]
        public string? UploadId { get; set; } // Tracks which import session created this contract (legacy)
        
        public int? ImportSessionId { get; set; } // Tracks which import session created or updated this contract
        
        public int? PvId { get; set; } // Optional link to PV (Ponto de Venda)

        [MaxLength(200)]
        public string? CustomerName { get; set; } // New field
        
        public int? ContractType { get; set; }
        
        public int? Quota { get; set; }
        
        public byte? Version { get; set; } // For contractDashboard import
        
        [MaxLength(100)]
        public string? RawStatus { get; set; } // Stores original status if mapped to NaoDefinido
        
        [MaxLength(50)]
        public string? TempMatricula { get; set; } // Temporary matricula reference
        
        public int? PlanoVendaMetadataId { get; set; } // Reference to ContractMetadata for Plano Venda
        
        public int? CategoryMetadataId { get; set; } // Reference to ContractMetadata for Category
        
        public int? MatriculaId { get; set; }
        
        // Navigation properties
        [JsonIgnore]
        public User? User { get; set; }
        public Matricula? Matricula { get; set; }
        public Group? Group { get; set; }
        public PV? PV { get; set; }
        public ContractMetadata? PlanoVendaMetadata { get; set; }
        public ContractMetadata? CategoryMetadata { get; set; }
        [JsonIgnore]
        public ImportSession? ImportSession { get; set; }
    }
}