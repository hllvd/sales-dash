using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ContractMetadata
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // e.g., "PlanoVenda", "Category"
        
        [Required]
        [MaxLength(100)]
        public string Value { get; set; } = string.Empty; // e.g., "Plan A", "LAR"
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ICollection<Contract> ContractsWithPlanoVenda { get; set; } = new List<Contract>();
        public ICollection<Contract> ContractsWithCategory { get; set; } = new List<Contract>();
    }
}
