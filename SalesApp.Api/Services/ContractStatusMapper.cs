using Microsoft.Extensions.Options;
using SalesApp.Models;
using SalesApp.Models.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesApp.Services
{
    /// <summary>
    /// Maps import status aliases to canonical ContractStatus values using JSON configuration
    /// </summary>
    public class ContractStatusMapper : IContractStatusMapper
    {
        private readonly Dictionary<string, string> _statusAliases;
        private readonly string[] _validStatuses;

        public ContractStatusMapper(IOptions<ContractStatusOptions> options)
        {
            _statusAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Build the flattened alias dictionary from the hierarchical configuration
            foreach (var mapping in options.Value.Mappings)
            {
                var canonical = mapping.Key;
                foreach (var alias in mapping.Value)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        // Map the alias to the canonical key
                        _statusAliases[alias.Trim()] = canonical.ToLowerInvariant();
                    }
                }
                
                // Also ensure the canonical keys themselves are in the dictionary
                if (!_statusAliases.ContainsKey(canonical))
                {
                    _statusAliases[canonical.Trim()] = canonical.ToLowerInvariant();
                }
            }

            // Valid statuses are the keys from the configuration
            _validStatuses = options.Value.Mappings.Keys
                .Select(k => k.ToLowerInvariant())
                .ToArray();
            
            // Fallback if config is empty (though it shouldn't be)
            if (_validStatuses.Length == 0)
            {
                _validStatuses = new[] 
                { 
                    ContractStatus.Active.ToApiString(), 
                    ContractStatus.Late1.ToApiString(), 
                    ContractStatus.Late2.ToApiString(), 
                    ContractStatus.Late3.ToApiString(), 
                    ContractStatus.Defaulted.ToApiString(),
                    ContractStatus.Transferred.ToApiString()
                };
            }
        }

        /// <summary>
        /// Maps an input status string to the canonical status value
        /// </summary>
        /// <param name="input">Input status string</param>
        /// <returns>Canonical status string or null if not found</returns>
        public string? MapStatus(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmed = input.Trim();
            return _statusAliases.TryGetValue(trimmed, out var canonical) ? canonical : null;
        }

        /// <summary>
        /// Validates if a status string is a valid canonical status or known alias
        /// </summary>
        /// <param name="status">Status string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValidStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var trimmed = status.Trim();

            // Accept canonical values (Active, Late1, etc.)
            if (_validStatuses.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                return true;

            // Also accept any known alias (Ativa, Ativo, CANCELADO, etc.)
            return _statusAliases.ContainsKey(trimmed);
        }

        /// <summary>
        /// Gets all valid canonical status values
        /// </summary>
        public string[] GetValidStatuses()
        {
            return _validStatuses;
        }
    }
}
