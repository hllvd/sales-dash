using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.CareerPlan;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerRuleEvaluatorTests
    {
        private readonly DateTime _refDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Evaluate_ResolvesTargetSalesGoalAndChecksProgress()
        {
            var targetLevel = new ClassificationLevel
            {
                Id = 2,
                Name = "Beth",
                SalesGoal = 4_000_000m
            };

            var rule = new RuleNode(
                Type: RuleNodeType.Condition,
                Label: "Produção 12 meses ≥ Meta",
                Metric: MetricKind.Production,
                WindowMonths: 12,
                Op: ComparisonOp.GreaterThanOrEqual,
                Value: new RuleValue(Reference: "targetLevel.salesGoal")
            );

            // User has 2_000_000m production
            var contracts = new List<Contract>
            {
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 2_000_000m,
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

            var result = CareerRuleEvaluator.Evaluate(rule, inputs, targetLevel);

            result.AllPassed.Should().BeFalse();
            result.Requirements.Should().HaveCount(1);
            var req = result.Requirements[0];
            req.Passed.Should().BeFalse();
            req.CurrentValue.Should().Be(2_000_000m);
            req.TargetValue.Should().Be(4_000_000m);
            req.Progress.Should().Be(0.5m);
            req.FormattedTarget.Should().Contain("4.000.000");
        }

        [Fact]
        public void Evaluate_WhenTargetRetentionDividedBy100()
        {
            var targetLevel = new ClassificationLevel
            {
                Id = 3,
                Name = "Guimel",
                Retention = 79 // 79% in database
            };

            var rule = new RuleNode(
                Type: RuleNodeType.Condition,
                Label: "Retenção ≥ Meta",
                Metric: MetricKind.StrictRetention,
                WindowMonths: 15,
                Op: ComparisonOp.GreaterThanOrEqual,
                Value: new RuleValue(Reference: "targetLevel.retention")
            );

            // User has 100% strict retention
            var contracts = new List<Contract>
            {
                new Contract
                {
                    SaleStartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalAmount = 100_000m,
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

            var result = CareerRuleEvaluator.Evaluate(rule, inputs, targetLevel);

            result.AllPassed.Should().BeTrue();
            result.Requirements[0].TargetValue.Should().Be(0.79m);
            result.Requirements[0].Passed.Should().BeTrue();
            result.Requirements[0].Progress.Should().Be(1.0m);
        }

        [Fact]
        public void Evaluate_WhenTargetFieldIsNull_ShowsUnconfiguredAndFails()
        {
            var targetLevel = new ClassificationLevel
            {
                Id = 3,
                Name = "Guimel",
                Retention = null // Not configured!
            };

            var rule = new RuleNode(
                Type: RuleNodeType.Condition,
                Metric: MetricKind.StrictRetention,
                WindowMonths: 15,
                Op: ComparisonOp.GreaterThanOrEqual,
                Value: new RuleValue(Reference: "targetLevel.retention")
            );

            var inputs = new CareerMetricInputs(
                UserContracts: new List<Contract>(),
                IsTeamOwner: false,
                ActiveTeamMembers: 0,
                DirectSubTeams: 0,
                ReferenceDate: _refDate
            );

            var result = CareerRuleEvaluator.Evaluate(rule, inputs, targetLevel);

            result.AllPassed.Should().BeFalse();
            result.Requirements[0].Passed.Should().BeFalse();
            result.Requirements[0].TargetValue.Should().BeNull();
            result.Requirements[0].FormattedTarget.Should().Be("Meta não configurada");
        }
    }
}
