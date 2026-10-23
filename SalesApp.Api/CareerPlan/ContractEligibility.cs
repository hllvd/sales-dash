using System;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public static class ContractEligibility
    {
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

        public static int CalculateInferredPaidMonths(int ageMonths, string? statusName)
        {
            var overdue = InferredOverdueMonths(statusName);
            return Math.Max(0, ageMonths - overdue);
        }

        public static bool IsEligible(Contract contract, EligibilityFilter? filter, DateTime referenceDate)
        {
            if (contract == null) return false;
            if (filter == null) return true;

            int? age = null;

            if (filter.MinAgeMonths.HasValue)
            {
                age = CareerReferenceDate.CalculateAgeMonths(contract.SaleStartDate, referenceDate);
                if (age < filter.MinAgeMonths.Value)
                {
                    return false;
                }
            }

            if (filter.MinInferredPaidMonths.HasValue)
            {
                age ??= CareerReferenceDate.CalculateAgeMonths(contract.SaleStartDate, referenceDate);
                var paid = CalculateInferredPaidMonths(age.Value, contract.ContractStatus?.Name);
                if (paid < filter.MinInferredPaidMonths.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
