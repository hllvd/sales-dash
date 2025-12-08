namespace SalesApp.Services
{
    /// <summary>
    /// Maps import status aliases to canonical ContractStatus values
    /// </summary>
    public class ContractStatusMapper
    {
        private static readonly Dictionary<string, string> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // Active aliases
            { "Active", "Active" },
            { "Normal", "Active" },
            
            // Late1 aliases
            { "Late1", "Late1" },
            { "NCONT 1 AT", "Late1" },
            
            // Late2 aliases
            { "Late2", "Late2" },
            { "NCONT 2 AT", "Late2" },
            
            // Late3 aliases
            { "Late3", "Late3" },
            { "NCONT 3 AT", "Late3" },
            { "SUJ. A CANCELAMENTO", "Late3" },
            { "SUJ. A  CANCELAMENTO", "Late3" }, // Handle double space
            
            // Defaulted aliases
            { "Defaulted", "Defaulted" },
            { "DESISTENTE", "Defaulted" },
            { "EXCLUIDO", "Defaulted" },
            { "paid_off", "Defaulted" }, // Legacy
            
            // Late3 legacy
            { "delinquent", "Late3" } // Legacy
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

            return status == "Active" || 
                   status == "Late1" || 
                   status == "Late2" || 
                   status == "Late3" || 
                   status == "Defaulted";
        }

        /// <summary>
        /// Gets all valid canonical status values
        /// </summary>
        public static string[] GetValidStatuses()
        {
            return new[] { "Active", "Late1", "Late2", "Late3", "Defaulted" };
        }
    }
}
