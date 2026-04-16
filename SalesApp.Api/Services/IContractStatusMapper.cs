using System.Collections.Generic;

namespace SalesApp.Services
{
    /// <summary>
    /// Service to map status aliases to canonical ContractStatus values
    /// </summary>
    public interface IContractStatusMapper
    {
        /// <summary>
        /// Maps an input status string to the canonical status value
        /// </summary>
        /// <param name="input">Input status string</param>
        /// <returns>Canonical status string or null if not found</returns>
        string? MapStatus(string? input);

        /// <summary>
        /// Validates if a status string is a valid canonical status or a known alias
        /// </summary>
        /// <param name="status">Status string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool IsValidStatus(string? status);

        /// <summary>
        /// Gets all valid canonical status values
        /// </summary>
        /// <returns>Array of canonical status strings</returns>
        string[] GetValidStatuses();
    }
}
