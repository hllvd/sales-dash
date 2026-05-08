using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    /// <summary>
    /// Lookup table for contract status values.
    /// Mirrors the ContractStatus enum but stored relationally for FK enforcement.
    /// </summary>
    public class ContractStatusEntity
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        // Navigation: contracts that carry this status (optional, add only if needed)
        // public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}
