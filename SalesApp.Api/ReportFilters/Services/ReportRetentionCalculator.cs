using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.Models;

namespace SalesApp.ReportFilters.Services
{
    /// <summary>
    /// Pure function helper for calculating consolidated overall retention rate.
    /// Follows strict pure, deterministic logic with isolated side effects.
    /// </summary>
    public static class ReportRetentionCalculator
    {
        /// <summary>
        /// Pure function to calculate overall retention rate on the full list of contracts.
        /// </summary>
        /// <param name="contracts">The raw list of contracts filtered for the report.</param>
        /// <param name="retentionType">The type of retention: standard or strict.</param>
        /// <returns>The calculated retention rate as a decimal, or 0 if total amount is 0 or list is empty.</returns>
        public static decimal CalculateOverallRetention(List<Contract> contracts, string? retentionType)
        {
            if (contracts == null || !contracts.Any())
            {
                return 0m;
            }

            var totalAmount = contracts.Sum(c => c.TotalAmount);
            if (totalAmount <= 0m)
            {
                return 0m;
            }

            var isStrict = string.Equals(retentionType, "strict", StringComparison.OrdinalIgnoreCase);
            decimal activeAmount;

            if (isStrict)
            {
                activeAmount = contracts
                    .Where(c => !string.Equals(
                        c.ContractStatus?.Name,
                        ContractStatus.Defaulted.ToApiString(),
                        StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(
                        c.ContractStatus?.Name,
                        ContractStatus.Late1.ToApiString(),
                        StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(
                        c.ContractStatus?.Name,
                        ContractStatus.Late2.ToApiString(),
                        StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(
                        c.ContractStatus?.Name,
                        ContractStatus.Late3.ToApiString(),
                        StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.TotalAmount);
            }
            else
            {
                activeAmount = contracts
                    .Where(c => !string.Equals(
                        c.ContractStatus?.Name,
                        ContractStatus.Defaulted.ToApiString(),
                        StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.TotalAmount);
            }

            return activeAmount / totalAmount;
        }
    }
}
