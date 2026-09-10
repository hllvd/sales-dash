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
        /// Pure predicate determining if a contract is currently in AwaitingPayment state.
        /// Contracts are awaiting payment if they have explicit status "AwaitingPayment"
        /// OR if status is "Active" and HasPayment is false.
        /// </summary>
        public static bool IsAwaitingPayment(Contract? c)
        {
            if (c == null) return false;
            return string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase)
                || (string.Equals(c.ContractStatus?.Name, ContractStatus.Active.ToApiString(), StringComparison.OrdinalIgnoreCase) && c.HasPayment == false);
        }

        /// <summary>
        /// Pure predicate determining if a contract is Desistente.
        /// Desistente contracts are never counted in retention metrics.
        /// </summary>
        public static bool IsDesistente(Contract? c)
        {
            if (c == null) return false;
            return string.Equals(c.ContractStatus?.Name, ContractStatus.Desistente.ToApiString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Pure function to calculate overall retention rate on the full list of contracts.
        /// </summary>
        /// <param name="contracts">The raw list of contracts filtered for the report.</param>
        /// <param name="retentionType">The type of retention: standard or strict.</param>
        /// <param name="includeAwaitingPayment">Whether to include awaiting payment contracts in retention metrics.</param>
        /// <returns>The calculated retention rate as a decimal, or 0 if total amount is 0 or list is empty.</returns>
        public static decimal CalculateOverallRetention(List<Contract> contracts, string? retentionType, bool includeAwaitingPayment = false)
        {
            if (contracts == null || !contracts.Any())
            {
                return 0m;
            }

            var totalAmount = contracts
                .Where(c => !IsDesistente(c) && (includeAwaitingPayment || !IsAwaitingPayment(c)))
                .Sum(c => c.TotalAmount);
            if (totalAmount <= 0m)
            {
                return 0m;
            }

            var isStrict = string.Equals(retentionType, "strict", StringComparison.OrdinalIgnoreCase);
            decimal activeAmount;

            if (isStrict)
            {
                activeAmount = contracts
                    .Where(c => !IsDesistente(c)
                             && (includeAwaitingPayment || !IsAwaitingPayment(c))
                             && !string.Equals(c.ContractStatus?.Name, ContractStatus.Defaulted.ToApiString(), StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late1.ToApiString(), StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late2.ToApiString(), StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late3.ToApiString(), StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.TotalAmount);
            }
            else
            {
                activeAmount = contracts
                    .Where(c => !IsDesistente(c)
                             && (includeAwaitingPayment || !IsAwaitingPayment(c))
                             && !string.Equals(c.ContractStatus?.Name, ContractStatus.Defaulted.ToApiString(), StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.TotalAmount);
            }

            return activeAmount / totalAmount;
        }
    }
}
