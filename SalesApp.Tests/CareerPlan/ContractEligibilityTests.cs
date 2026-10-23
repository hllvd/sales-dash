using System;
using FluentAssertions;
using SalesApp.CareerPlan;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class ContractEligibilityTests
    {
        [Theory]
        [InlineData("Active", 0)]
        [InlineData("active", 0)]
        [InlineData("Late1", 1)]
        [InlineData("Late2", 2)]
        [InlineData("Late3", 3)]
        [InlineData("Defaulted", 0)]
        [InlineData(null, 0)]
        public void InferredOverdueMonths_MapsCorrectly(string? status, int expected)
        {
            ContractEligibility.InferredOverdueMonths(status).Should().Be(expected);
        }

        [Theory]
        [InlineData(3, "Late1", 2)]
        [InlineData(3, "Active", 3)]
        [InlineData(2, "Late3", 0)] // Cannot be negative
        [InlineData(0, "Active", 0)]
        public void CalculateInferredPaidMonths_SubtractsOverdueFromAge(int age, string status, int expectedPaid)
        {
            ContractEligibility.CalculateInferredPaidMonths(age, status).Should().Be(expectedPaid);
        }

        [Fact]
        public void IsEligible_WhenFilterIsNull_ReturnsTrue()
        {
            var contract = new Contract { SaleStartDate = DateTime.UtcNow };
            ContractEligibility.IsEligible(contract, null, DateTime.UtcNow).Should().BeTrue();
        }

        [Fact]
        public void IsEligible_WhenMinAgeMonthsSpecified_FiltersYoungerContracts()
        {
            var refDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
            var filter = new EligibilityFilter(MinAgeMonths: 2);

            // Contract 1 month old -> false
            var youngContract = new Contract
            {
                SaleStartDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)
            };
            ContractEligibility.IsEligible(youngContract, filter, refDate).Should().BeFalse();

            // Contract 2 months old -> true
            var matureContract = new Contract
            {
                SaleStartDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
            };
            ContractEligibility.IsEligible(matureContract, filter, refDate).Should().BeTrue();
        }

        [Fact]
        public void IsEligible_WhenMinInferredPaidMonthsSpecified_ChecksPaidCount()
        {
            var refDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
            var filter = new EligibilityFilter(MinInferredPaidMonths: 2);

            // Age 2 months but Late1 -> 2 - 1 = 1 paid month -> false
            var lateContract = new Contract
            {
                SaleStartDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                ContractStatus = new ContractStatusEntity { Name = "Late1" }
            };
            ContractEligibility.IsEligible(lateContract, filter, refDate).Should().BeFalse();

            // Age 2 months and Active -> 2 - 0 = 2 paid months -> true
            var activeContract = new Contract
            {
                SaleStartDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                ContractStatus = new ContractStatusEntity { Name = "Active" }
            };
            ContractEligibility.IsEligible(activeContract, filter, refDate).Should().BeTrue();
        }
    }
}
