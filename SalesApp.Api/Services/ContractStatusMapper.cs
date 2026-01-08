using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Maps import status aliases to canonical ContractStatus values
    /// </summary>
    public class ContractStatusMapper
    {
        private static readonly Dictionary<string, string> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // ✅ Active aliases
            { "Active", ContractStatus.Active.ToApiString() },
            { "Normal", ContractStatus.Active.ToApiString() },
            
            // ✅ Late1 aliases
            { "Late1", ContractStatus.Late1.ToApiString() },
            { "NCONT 1 AT", ContractStatus.Late1.ToApiString() },
            
            // ✅ Late2 aliases
            { "Late2", ContractStatus.Late2.ToApiString() },
            { "NCONT 2 AT", ContractStatus.Late2.ToApiString() },
            
            // ✅ Late3 aliases
            { "Late3", ContractStatus.Late3.ToApiString() },
            { "NCONT 3 AT", ContractStatus.Late3.ToApiString() },
            { "SUJ. A CANCELAMENTO", ContractStatus.Late3.ToApiString() },
            { "SUJ. A  CANCELAMENTO", ContractStatus.Late3.ToApiString() }, // Handle double space
            
            // ✅ Defaulted aliases
            { "Defaulted", ContractStatus.Defaulted.ToApiString() },
            { "DESISTENTE", ContractStatus.Defaulted.ToApiString() },
            { "EXCLUIDO", ContractStatus.Defaulted.ToApiString() },
            { "paid_off", ContractStatus.Defaulted.ToApiString() }, // Legacy
            
            // Late3 legacy
            { "delinquent", ContractStatus.Late3.ToApiString() } // Legacy
        };

        /// <summary>
        /// Maps an input status string to the canonical status value
        /// </summary>
        /// <param name="input">Input status string</param>
        /// <returns>Canonical status string or null if not found</returns>
        public static string? MapStatus(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmed = input.Trim();
            return StatusAliases.TryGetValue(trimmed, out var canonical) ? canonical : null;
        }

        /// <summary>
        /// Validates if a status string is a valid canonical status
        /// </summary>
        /// <param name="status">Status string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            // ✅ Use enum instead of hardcoded strings
            return status == ContractStatus.Active.ToApiString() || 
                   status == ContractStatus.Late1.ToApiString() || 
                   status == ContractStatus.Late2.ToApiString() || 
                   status == ContractStatus.Late3.ToApiString() || 
                   status == ContractStatus.Defaulted.ToApiString();
        }

        /// <summary>
        /// Gets all valid canonical status values
        /// </summary>
        public static string[] GetValidStatuses()
        {
            // ✅ Use enum instead of hardcoded strings
            return new[] 
            { 
                ContractStatus.Active.ToApiString(), 
                ContractStatus.Late1.ToApiString(), 
                ContractStatus.Late2.ToApiString(), 
                ContractStatus.Late3.ToApiString(), 
                ContractStatus.Defaulted.ToApiString() 
            };
        }
    }
}
