using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public enum RtCellState
    {
        Blank,
        Paid,
        Unpaid
    }

    public enum RtCurrentState
    {
        UpToDate,
        Overdue
    }

    public record ContractRtEvaluation(
        string ContractNumber,
        string? CustomerName,
        decimal TotalAmount,
        DateTime SaleStartDate,
        string Status,
        IReadOnlyList<RtCellState> Rts,
        RtCurrentState Current,
        int AgeMonths,
        int OverdueMonths,
        int PaidMonths
    );

    public static class ContractPaymentTracking
    {
        private static readonly HashSet<string> EligibleLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Alef",
            "Beth",
            "Guimel"
        };

        private static readonly HashSet<string> ExcludedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Desistente",
            "AwaitingPayment"
        };

        /// <summary>
        /// Pure function checking if a classification level qualifies for viewing the RT table.
        /// Eligible: unclassified (null/empty, competing for Alef), Alef, Beth, Guimel.
        /// Ineligible: Daleth and above.
        /// </summary>
        public static bool IsLevelEligibleForRtTable(string? levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return true;
            }

            return EligibleLevels.Contains(levelName.Trim());
        }

        /// <summary>
        /// Pure function inferring overdue months for RT evaluation:
        /// Late1 -> 1, Late2 -> 2, Late3 -> 3, Defaulted -> 4, others -> 0.
        /// </summary>
        public static int InferredRtOverdueMonths(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName)) return 0;

            return statusName.Trim().ToLowerInvariant() switch
            {
                "late1" => 1,
                "late2" => 2,
                "late3" => 3,
                "defaulted" => 4,
                _ => 0
            };
        }

        /// <summary>
        /// Pure function calculating the state of RT1..RT{count} cells:
        /// - Blank: month index > age
        /// - Paid: month index <= paidMonths
        /// - Unpaid: paidMonths < month index <= age
        /// </summary>
        public static IReadOnlyList<RtCellState> CalculateRtCells(int ageMonths, int overdueMonths, int count = 6)
        {
            if (count < 0) count = 0;
            int safeAge = Math.Max(0, ageMonths);
            int effectiveOverdue = Math.Min(safeAge, Math.Max(0, overdueMonths));
            int paidMonths = Math.Max(0, safeAge - effectiveOverdue);

            var cells = new List<RtCellState>(count);
            for (int i = 1; i <= count; i++)
            {
                if (i > safeAge)
                {
                    cells.Add(RtCellState.Blank);
                }
                else if (i <= paidMonths)
                {
                    cells.Add(RtCellState.Paid);
                }
                else
                {
                    cells.Add(RtCellState.Unpaid);
                }
            }

            return cells;
        }

        /// <summary>
        /// Pure function resolving RT Atual:
        /// - Overdue (red) if overdueMonths > 0
        /// - UpToDate (green) if overdueMonths == 0
        /// </summary>
        public static RtCurrentState ResolveRtCurrent(int overdueMonths)
        {
            return overdueMonths > 0 ? RtCurrentState.Overdue : RtCurrentState.UpToDate;
        }

        /// <summary>
        /// Pure function checking if a contract is eligible for the RT tracking table:
        /// - Must not be null
        /// - HasPayment != false
        /// - Status not in ExcludedStatuses (Desistente, AwaitingPayment)
        /// - SaleStartDate within the last windowMonths (default 15) relative to referenceDate
        /// </summary>
        public static bool IsEligibleContract(Contract contract, DateTime referenceDate, int windowMonths = 15)
        {
            if (contract == null) return false;
            if (contract.HasPayment == false) return false;

            var statusName = contract.ContractStatus?.Name;
            if (!string.IsNullOrWhiteSpace(statusName) && ExcludedStatuses.Contains(statusName))
            {
                return false;
            }

            var minDate = referenceDate.AddMonths(-windowMonths);
            if (contract.SaleStartDate < minDate)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Pure function evaluating a single contract for RT tracking.
        /// </summary>
        public static ContractRtEvaluation BuildRtRow(Contract contract, DateTime referenceDate)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            var statusName = contract.ContractStatus?.Name ?? "Unknown";
            int age = CareerReferenceDate.CalculateAgeMonths(contract.SaleStartDate, referenceDate);
            int overdue = InferredRtOverdueMonths(statusName);
            int effectiveOverdue = Math.Min(age, overdue);
            int paid = Math.Max(0, age - effectiveOverdue);

            var rts = CalculateRtCells(age, overdue, 6);
            var current = ResolveRtCurrent(overdue);

            return new ContractRtEvaluation(
                ContractNumber: contract.ContractNumber ?? string.Empty,
                CustomerName: contract.CustomerName,
                TotalAmount: contract.TotalAmount,
                SaleStartDate: contract.SaleStartDate,
                Status: statusName,
                Rts: rts,
                Current: current,
                AgeMonths: age,
                OverdueMonths: overdue,
                PaidMonths: paid
            );
        }

        /// <summary>
        /// Pure function filtering, evaluating and ordering user contracts for the RT table.
        /// </summary>
        public static IReadOnlyList<ContractRtEvaluation> EvaluateContracts(
            IEnumerable<Contract> contracts,
            DateTime referenceDate,
            int windowMonths = 15)
        {
            if (contracts == null) return Array.Empty<ContractRtEvaluation>();

            return contracts
                .Where(c => IsEligibleContract(c, referenceDate, windowMonths))
                .OrderByDescending(c => c.SaleStartDate)
                .ThenByDescending(c => c.Id)
                .Select(c => BuildRtRow(c, referenceDate))
                .ToList();
        }
    }
}
