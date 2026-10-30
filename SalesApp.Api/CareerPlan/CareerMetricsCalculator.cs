using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.Models;
using SalesApp.ReportFilters.Services;

namespace SalesApp.CareerPlan
{
    public record CareerMetricInputs(
        IReadOnlyList<Contract> UserContracts,
        bool IsTeamOwner,
        int ActiveTeamMembers,
        int DirectSubTeams,
        DateTime ReferenceDate
    );

    public record CareerRawMetrics(
        decimal Retention6m,
        decimal Retention12m,
        decimal Retention15m,
        decimal StrictRetention6m,
        decimal StrictRetention12m,
        decimal StrictRetention15m,
        decimal StrictRetention15mMinAge2,
        decimal Production12m,
        decimal Production15m,
        decimal ActiveProduction12mMinPaid2,
        bool IsTeamOwner,
        int ActiveTeamMembers,
        int DirectSubTeams
    );

    public static class CareerMetricsCalculator
    {
        public static decimal ComputeActiveProduction(
            int windowMonths,
            int minInferredPaidMonths,
            CareerMetricInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var (start, end) = CareerReferenceDate.GetClosedMonthWindow(inputs.ReferenceDate, windowMonths);

            return inputs.UserContracts
                .Where(c => c.SaleStartDate >= start && c.SaleStartDate < end)
                .Where(c => string.Equals(c.ContractStatus?.Name, ContractStatus.Active.ToApiString(), StringComparison.OrdinalIgnoreCase))
                .Where(c => c.HasPayment != false)
                .Where(c =>
                {
                    int age = CareerReferenceDate.CalculateAgeMonths(c.SaleStartDate, inputs.ReferenceDate);
                    int paid = ContractEligibility.CalculateInferredPaidMonths(age, c.ContractStatus?.Name);
                    return paid >= minInferredPaidMonths;
                })
                .Sum(c => c.TotalAmount);
        }

        public static decimal ComputeMetric(
            MetricKind metric,
            int? windowMonths,
            EligibilityFilter? eligibility,
            CareerMetricInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            switch (metric)
            {
                case MetricKind.IsTeamOwner:
                    return inputs.IsTeamOwner ? 1m : 0m;

                case MetricKind.ActiveTeamMembers:
                    return inputs.ActiveTeamMembers;

                case MetricKind.DirectSubTeams:
                    return inputs.DirectSubTeams;

                case MetricKind.Retention:
                case MetricKind.StrictRetention:
                {
                    int wm = windowMonths.GetValueOrDefault(12);
                    var (start, end) = CareerReferenceDate.GetClosedMonthWindow(inputs.ReferenceDate, wm);

                    var eligibleContracts = inputs.UserContracts
                        .Where(c => c.SaleStartDate >= start && c.SaleStartDate < end)
                        .Where(c => ContractEligibility.IsEligible(c, eligibility, inputs.ReferenceDate))
                        .ToList();

                    var retentionType = metric == MetricKind.StrictRetention ? "strict" : "standard";
                    return ReportRetentionCalculator.CalculateOverallRetention(eligibleContracts, retentionType, includeAwaitingPayment: false);
                }

                case MetricKind.Production:
                {
                    int wm = windowMonths.GetValueOrDefault(12);
                    var (start, end) = CareerReferenceDate.GetClosedMonthWindow(inputs.ReferenceDate, wm);

                    var eligibleProduction = inputs.UserContracts
                        .Where(c => c.SaleStartDate >= start && c.SaleStartDate < end)
                        .Where(c => ContractEligibility.IsEligible(c, eligibility, inputs.ReferenceDate))
                        .Where(c => !ReportRetentionCalculator.IsDesistente(c) && !ReportRetentionCalculator.IsAwaitingPayment(c))
                        .Sum(c => c.TotalAmount);

                    return eligibleProduction;
                }

                default:
                    throw new NotSupportedException($"MetricKind '{metric}' is not supported.");
            }
        }

        public static CareerRawMetrics ComputeRawMetrics(CareerMetricInputs inputs)
        {
            var r6 = ComputeMetric(MetricKind.Retention, 6, null, inputs);
            var r12 = ComputeMetric(MetricKind.Retention, 12, null, inputs);
            var r15 = ComputeMetric(MetricKind.Retention, 15, null, inputs);

            var sr6 = ComputeMetric(MetricKind.StrictRetention, 6, null, inputs);
            var sr12 = ComputeMetric(MetricKind.StrictRetention, 12, null, inputs);
            var sr15 = ComputeMetric(MetricKind.StrictRetention, 15, null, inputs);

            var sr15Age2 = ComputeMetric(MetricKind.StrictRetention, 15, new EligibilityFilter(MinAgeMonths: 2), inputs);

            var p12 = ComputeMetric(MetricKind.Production, 12, null, inputs);
            var p15 = ComputeMetric(MetricKind.Production, 15, null, inputs);

            var activeProd12mMinPaid2 = ComputeActiveProduction(12, 2, inputs);

            return new CareerRawMetrics(
                Retention6m: r6,
                Retention12m: r12,
                Retention15m: r15,
                StrictRetention6m: sr6,
                StrictRetention12m: sr12,
                StrictRetention15m: sr15,
                StrictRetention15mMinAge2: sr15Age2,
                Production12m: p12,
                Production15m: p15,
                ActiveProduction12mMinPaid2: activeProd12mMinPaid2,
                IsTeamOwner: inputs.IsTeamOwner,
                ActiveTeamMembers: inputs.ActiveTeamMembers,
                DirectSubTeams: inputs.DirectSubTeams
            );
        }
    }
}
