using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.CareerPlan;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class ContractPaymentTrackingTests
    {
        private readonly DateTime _refDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("Alef", true)]
        [InlineData("alef", true)]
        [InlineData("Beth", true)]
        [InlineData("beth", true)]
        [InlineData("Guimel", true)]
        [InlineData("GUIMEL", true)]
        [InlineData("Daleth", false)]
        [InlineData("Vav", false)]
        [InlineData("Hé", false)]
        [InlineData("Lamed", false)]
        public void IsLevelEligibleForRtTable_EvaluatesLevelsCorrectly(string? levelName, bool expected)
        {
            var result = ContractPaymentTracking.IsLevelEligibleForRtTable(levelName);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Active", 0)]
        [InlineData("active", 0)]
        [InlineData("Late1", 1)]
        [InlineData("late1", 1)]
        [InlineData("Late2", 2)]
        [InlineData("Late3", 3)]
        [InlineData("Defaulted", 4)]
        [InlineData("defaulted", 4)]
        [InlineData("Transferred", 0)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        public void InferredRtOverdueMonths_MapsStatusesCorrectly(string? statusName, int expectedOverdue)
        {
            var result = ContractPaymentTracking.InferredRtOverdueMonths(statusName);
            result.Should().Be(expectedOverdue);
        }

        [Fact]
        public void CalculateRtCells_4MonthsLate1_Has3Paid1Unpaid2BlankAndOverdue()
        {
            // 4 months old, Late1 (1 month overdue)
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 4, overdueMonths: 1, count: 6);
            cells.Should().Equal(
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Unpaid,
                RtCellState.Blank,
                RtCellState.Blank
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 1);
            current.Should().Be(RtCurrentState.Overdue);
        }

        [Fact]
        public void CalculateRtCells_8MonthsLate1_Has6PaidAndCurrentOverdue()
        {
            // 8 months old, Late1 -> 7 paid months. First 6 RTs are paid, but RT Current is Overdue
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 8, overdueMonths: 1, count: 6);
            cells.Should().Equal(
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 1);
            current.Should().Be(RtCurrentState.Overdue);
        }

        [Fact]
        public void CalculateRtCells_8MonthsActive_Has6PaidAndCurrentUpToDate()
        {
            // 8 months old, Active (0 overdue) -> 8 paid months. All 6 RTs paid, Current UpToDate
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 8, overdueMonths: 0, count: 6);
            cells.Should().Equal(
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 0);
            current.Should().Be(RtCurrentState.UpToDate);
        }

        [Fact]
        public void CalculateRtCells_1MonthActive_Has1Paid5BlankAndCurrentUpToDate()
        {
            // 1 month old, Active -> 1 paid, rest blank, Current UpToDate
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 1, overdueMonths: 0, count: 6);
            cells.Should().Equal(
                RtCellState.Paid,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 0);
            current.Should().Be(RtCurrentState.UpToDate);
        }

        [Fact]
        public void CalculateRtCells_1MonthLate1_Has1Unpaid5BlankAndCurrentOverdue()
        {
            // 1 month old, Late1 -> 0 paid, 1 unpaid, 5 blank, Current Overdue
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 1, overdueMonths: 1, count: 6);
            cells.Should().Equal(
                RtCellState.Unpaid,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank,
                RtCellState.Blank
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 1);
            current.Should().Be(RtCurrentState.Overdue);
        }

        [Fact]
        public void CalculateRtCells_7MonthsDefaulted_Has3Paid3UnpaidAndCurrentOverdue()
        {
            // 7 months old, Defaulted (4 months overdue) -> 3 paid (RT1..3), 3 unpaid (RT4..6), Current Overdue
            var cells = ContractPaymentTracking.CalculateRtCells(ageMonths: 7, overdueMonths: 4, count: 6);
            cells.Should().Equal(
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Paid,
                RtCellState.Unpaid,
                RtCellState.Unpaid,
                RtCellState.Unpaid
            );

            var current = ContractPaymentTracking.ResolveRtCurrent(overdueMonths: 4);
            current.Should().Be(RtCurrentState.Overdue);
        }

        [Fact]
        public void IsEligibleContract_FiltersExcludedStatusesAndOldContracts()
        {
            var valid = new Contract
            {
                SaleStartDate = _refDate.AddMonths(-10),
                HasPayment = true,
                ContractStatus = new ContractStatusEntity { Name = "Active" }
            };
            ContractPaymentTracking.IsEligibleContract(valid, _refDate).Should().BeTrue();

            var desistente = new Contract
            {
                SaleStartDate = _refDate.AddMonths(-5),
                HasPayment = true,
                ContractStatus = new ContractStatusEntity { Name = "Desistente" }
            };
            ContractPaymentTracking.IsEligibleContract(desistente, _refDate).Should().BeFalse();

            var awaiting = new Contract
            {
                SaleStartDate = _refDate.AddMonths(-5),
                HasPayment = true,
                ContractStatus = new ContractStatusEntity { Name = "AwaitingPayment" }
            };
            ContractPaymentTracking.IsEligibleContract(awaiting, _refDate).Should().BeFalse();

            var noPayment = new Contract
            {
                SaleStartDate = _refDate.AddMonths(-5),
                HasPayment = false,
                ContractStatus = new ContractStatusEntity { Name = "Active" }
            };
            ContractPaymentTracking.IsEligibleContract(noPayment, _refDate).Should().BeFalse();

            var tooOld = new Contract
            {
                SaleStartDate = _refDate.AddMonths(-16),
                HasPayment = true,
                ContractStatus = new ContractStatusEntity { Name = "Active" }
            };
            ContractPaymentTracking.IsEligibleContract(tooOld, _refDate).Should().BeFalse();
        }

        [Fact]
        public void EvaluateContracts_OrdersBySaleStartDateDescendingAndBuildsRows()
        {
            var contracts = new List<Contract>
            {
                new Contract
                {
                    Id = 1,
                    ContractNumber = "CTR-OLD",
                    CustomerName = "Cliente 1",
                    TotalAmount = 50_000m,
                    SaleStartDate = _refDate.AddMonths(-10),
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Active" }
                },
                new Contract
                {
                    Id = 2,
                    ContractNumber = "CTR-NEW",
                    CustomerName = "Cliente 2",
                    TotalAmount = 120_000m,
                    SaleStartDate = _refDate.AddMonths(-2),
                    HasPayment = true,
                    ContractStatus = new ContractStatusEntity { Name = "Late1" }
                }
            };

            var evaluated = ContractPaymentTracking.EvaluateContracts(contracts, _refDate);

            evaluated.Should().HaveCount(2);
            evaluated[0].ContractNumber.Should().Be("CTR-NEW");
            evaluated[0].Status.Should().Be("Late1");
            evaluated[0].Current.Should().Be(RtCurrentState.Overdue);

            evaluated[1].ContractNumber.Should().Be("CTR-OLD");
            evaluated[1].Status.Should().Be("Active");
            evaluated[1].Current.Should().Be(RtCurrentState.UpToDate);
        }
    }
}
