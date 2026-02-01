namespace SalesApp.Models
{
    /// <summary>
    /// Contract status enumeration
    /// </summary>
    public enum ContractStatus
    {
        /// <summary>
        /// Customer is paying regularly
        /// </summary>
        Active,
        
        /// <summary>
        /// 1 month late
        /// </summary>
        Late1,
        
        /// <summary>
        /// 2 months late
        /// </summary>
        Late2,
        
        /// <summary>
        /// 3 months late
        /// </summary>
        Late3,
        
        /// <summary>
        /// Inactive/defaulted
        /// </summary>
        Defaulted
    }

    /// <summary>
    /// Extension methods for ContractStatus enum
    /// </summary>
    public static class ContractStatusExtensions
    {
        /// <summary>
        /// Converts ContractStatus enum to API string representation
        /// </summary>
        public static string ToApiString(this ContractStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Converts API string to ContractStatus enum (case-insensitive)
        /// </summary>
        public static ContractStatus FromApiString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Contract status cannot be null or empty");

            return value.Trim() switch
            {
                "Active" or "active" => ContractStatus.Active,
                "Late1" or "late1" => ContractStatus.Late1,
                "Late2" or "late2" => ContractStatus.Late2,
                "Late3" or "late3" => ContractStatus.Late3,
                "Defaulted" or "defaulted" => ContractStatus.Defaulted,
                _ => throw new ArgumentException($"Invalid contract status: {value}. Valid values are: Active, Late1, Late2, Late3, Defaulted")
            };
        }

        /// <summary>
        /// Validates if a string is a valid contract status
        /// </summary>
        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                FromApiString(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
