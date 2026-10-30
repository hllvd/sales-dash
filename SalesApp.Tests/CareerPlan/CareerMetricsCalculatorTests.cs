using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.CareerPlan;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerMetricsCalculatorTests
    {
        private readonly DateTime _refDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ComputeMetric_Production_CalculatesAmountExcludingDesistenteAndAwaitingPayment()
        {
            var contracts = new List<Contract>
            {
                // Active, has payment, in 12m window -> Counted (100k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // Late1, in 12m window -> Counted in production (50k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 50_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Late1" }
                },
                // Desistente, in 12m window -> EXCLUDED from production (30k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 30_000m,
                    HasPayment = false,
                    ContractStatus = new ContractStatusEntity { Name = "Desistente" }
                },
                // AwaitingPayment explicit -> EXCLUDED (20k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 20_000m,
                    ContractStatus = new ContractStatusEntity { Name = "AwaitingPayment" }
                },
                // Active but HasPayment == false -> EXCLUDED as awaiting payment (40k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 40_000m,
                    HasPayment = false,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // Outside 12m window (Sale in 2025-05) -> EXCLUDED (80k)
                new Contract
                {
                    SaleStartDate = new DateTime(2025, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 80_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                }
            };

            var inputs = new CareerMetricInputs(
                UserContracts: contracts,
                IsTeamOwner: false,
                ActiveTeamMembers: 0,
                DirectSubTeams: 0,
                ReferenceDate: _refDate
            );

            var production = CareerMetricsCalculator.ComputeMetric(MetricKind.Production, 12, null, inputs);

            // Expected: 100,000 + 50,000 = 150,000
            production.Should().Be(150_000m);
        }

        [Fact]
        public void ComputeMetric_StrictRetention_ConsidersLateAsLost()
        {
            var contracts = new List<Contract>
            {
                // Active 100k
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // Late1 100k
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Late1" }
                }
            };

            var inputs = new CareerMetricInputs(
                UserContracts: contracts,
                IsTeamOwner: false,
                ActiveTeamMembers: 0,
                DirectSubTeams: 0,
                ReferenceDate: _refDate
            );

            // Strict retention: Active (100k) / Total (200k) = 0.50 (50%)
            var strict = CareerMetricsCalculator.ComputeMetric(MetricKind.StrictRetention, 12, null, inputs);
            strict.Should().Be(0.50m);

            // Standard retention: (Active + Late1) / Total = 200k / 200k = 1.0 (100%)
            var standard = CareerMetricsCalculator.ComputeMetric(MetricKind.Retention, 12, null, inputs);
            standard.Should().Be(1.00m);
        }

        [Fact]
        public void ComputeMetric_StrictRetentionWithMinAge2_ExcludesRecentContracts()
        {
            var contracts = new List<Contract>
            {
                // Sold 2026-08-10 (Age = 2 months relative to 2026-10-15) -> Included, Active 100k
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // Sold 2026-09-10 (Age = 1 month) -> EXCLUDED by minAgeMonths: 2 (was Late1 100k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Late1" }
                }
            };

            var inputs = new CareerMetricInputs(
                UserContracts: contracts,
                IsTeamOwner: false,
                ActiveTeamMembers: 0,
                DirectSubTeams: 0,
                ReferenceDate: _refDate
            );

            var filter = new EligibilityFilter(MinAgeMonths: 2);
            var strict = CareerMetricsCalculator.ComputeMetric(MetricKind.StrictRetention, 15, filter, inputs);

            // With filter, only the 100k Active contract is included -> 100%
            strict.Should().Be(1.0m);
        }

        [Fact]
        public void ComputeMetric_TeamMetrics_ReturnsDirectValues()
        {
            var inputs = new CareerMetricInputs(
                UserContracts: new List<Contract>(),
                IsTeamOwner: true,
                ActiveTeamMembers: 14,
                DirectSubTeams: 3,
                ReferenceDate: _refDate
            );

            CareerMetricsCalculator.ComputeMetric(MetricKind.IsTeamOwner, null, null, inputs).Should().Be(1m);
            CareerMetricsCalculator.ComputeMetric(MetricKind.ActiveTeamMembers, null, null, inputs).Should().Be(14m);
            CareerMetricsCalculator.ComputeMetric(MetricKind.DirectSubTeams, null, null, inputs).Should().Be(3m);
        }

        [Fact]
        public void ComputeRawMetrics_CalculatesActiveProduction12mMinPaid2_Correctly()
        {
            var contracts = new List<Contract>
            {
                // 1. Sold 2026-07-10 (Age 3m, Active, HasPayment = true) -> 3 paid >= 2 -> INCLUDED (100k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // 2. Sold 2026-09-10 (Age 1m, Active, HasPayment = true) -> 1 paid < 2 -> EXCLUDED (50k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 50_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // 3. Sold 2026-07-10 (Age 3m, Late1) -> 3 - 1 = 2 paid >= 2, but status is Late1 (not Active) -> EXCLUDED (70k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 70_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Late1" }
                },
                // 4. Sold 2026-06-10 (Age 4m, Active, HasPayment = false) -> EXCLUDED (40k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 40_000m,
                    HasPayment = false,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                // 5. Sold 2026-05-10 (Age 5m, Defaulted) -> EXCLUDED (80k)
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 80_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Defaulted" }
                },
                // 6. Sold 2025-05-10 (Outside 12m window) -> EXCLUDED (200k)
                new Contract
                {
                    SaleStartDate = new DateTime(2025, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 200_000m,
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                }
            };

            var inputs = new CareerMetricInputs(
                UserContracts: contracts,
                IsTeamOwner: false,
                ActiveTeamMembers: 0,
                DirectSubTeams: 0,
                ReferenceDate: _refDate
            );

            var raw = CareerMetricsCalculator.ComputeRawMetrics(inputs);

            // Only Contract 1 qualifies for ActiveProduction12mMinPaid2 -> 100_000m
            raw.ActiveProduction12mMinPaid2.Should().Be(100_000m);
        }
    }
}
