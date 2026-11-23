using System;
using System.Collections.Generic;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public static class ContractEligibility
    {
        private static readonly HashSet<string> NonPaidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Desistente",
            "AwaitingPayment"
        };

        public static int InferredOverdueMonths(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName)) return 0;

            return statusName.Trim().ToLowerInvariant() switch
            {
                "late1" => 1,
                "late2" => 2,
                "late3" => 3,
                _ => 0
            };
        }

        public static int CalculateInferredPaidMonths(int dueInstallments, string? statusName, bool hasPayment = true)
        {
            int safeDue = Math.Max(0, dueInstallments);
            int overdue = InferredOverdueMonths(statusName);
            int minPaidMonths = CanInferFirstInstallmentPaid(statusName, hasPayment) ? 1 : 0;
            return Math.Max(minPaidMonths, safeDue - overdue);
        }

        public static int CalculateInferredPaidMonths(DateTime saleDate, string? statusName, DateTime referenceDate, bool hasPayment = true)
        {
            int due = CareerReferenceDate.CalculateDueInstallments(saleDate, referenceDate);
            return CalculateInferredPaidMonths(due, statusName, hasPayment);
        }

        public static int CalculateInferredPaidMonths(Contract contract, DateTime referenceDate)
        {
            if (contract == null) return 0;
            if (contract.SimulatedPaidMonths.HasValue)
            {
                return contract.SimulatedPaidMonths.Value;
            }
            return CalculateInferredPaidMonths(contract.SaleStartDate, contract.ContractStatus?.Name, referenceDate, contract.HasPayment != false);
        }

        private static bool CanInferFirstInstallmentPaid(string? statusName, bool hasPayment)
        {
            if (!hasPayment) return false;
            return string.IsNullOrWhiteSpace(statusName) || !NonPaidStatuses.Contains(statusName.Trim());
        }

        public static bool IsEligible(Contract contract, EligibilityFilter? filter, DateTime referenceDate)
        {
            if (contract == null || !contract.IsActive) return false;
            if (filter == null) return true;

            if (filter.MinAgeMonths.HasValue)
            {
                int age = CareerReferenceDate.CalculateAgeMonths(contract.SaleStartDate, referenceDate);
                if (age < filter.MinAgeMonths.Value)
                {
                    return false;
                }
            }

            if (filter.MinInferredPaidMonths.HasValue)
            {
                var paid = CalculateInferredPaidMonths(contract, referenceDate);
                if (paid < filter.MinInferredPaidMonths.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
