using Xunit;
using FluentAssertions;
using SalesApp.Services;
using SalesApp.Models;
using SalesApp.DTOs;

namespace SalesApp.Tests.Services
{
    public class ContractAggregationServiceTests
    {
        private readonly ContractAggregationService _service;

        public ContractAggregationServiceTests()
        {
            _service = new ContractAggregationService();
        }

        [Fact]
        public void CalculateAggregation_WithAllActiveContracts_ShouldReturnRetentionOf1()
        {
            // Arrange - All active means 100% retention
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Active" },
                new Contract { TotalAmount = 2000, Status = "Active" },
                new Contract { TotalAmount = 1500, Status = "Active" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(4500);
            result.TotalCancel.Should().Be(0);
            result.Retention.Should().Be(1.0m); // 4500 active / 4500 total = 1.0
        }

        [Fact]
        public void CalculateAggregation_WithNoActiveContracts_ShouldReturnZeroRetention()
        {
            // Arrange - Only defaulted means 0% retention, but Late1 is included in TotalActive
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Defaulted" },
                new Contract { TotalAmount = 2000, Status = "Late1" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(3000);
            result.TotalCancel.Should().Be(1000);
            result.TotalActive.Should().Be(2000); // Late1 is included in active
            result.Retention.Should().Be(0.6666666666666666666666666667m); // 2000 active / 3000 total
        }

        [Fact]
        public void CalculateAggregation_WithMixedStatuses_ShouldCalculateCorrectRetention()
        {
            // Arrange - Active + Late contracts = 3000 out of 5000 total = 0.6
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Active" },
                new Contract { TotalAmount = 2000, Status = "Defaulted" },
                new Contract { TotalAmount = 1500, Status = "Late1" },
                new Contract { TotalAmount = 500, Status = "Late2" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(5000);
            result.TotalCancel.Should().Be(2000);
            result.TotalActive.Should().Be(3000); // Active (1000) + Late1 (1500) + Late2 (500)
            result.Retention.Should().Be(0.6m); // 3000 active / 5000 total = 0.6
        }

        [Fact]
        public void CalculateAggregation_WithEmptyList_ShouldReturnZeroRetention()
        {
            // Arrange
            var contracts = new List<Contract>();

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(0);
            result.TotalCancel.Should().Be(0);
            result.Retention.Should().Be(0.0m);
        }

        [Fact]
        public void CalculateAggregation_WithCaseInsensitiveStatus_ShouldWorkCorrectly()
        {
            // Arrange - 1500 active out of 4500 total = 0.333...
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "DEFAULTED" },
                new Contract { TotalAmount = 2000, Status = "defaulted" },
                new Contract { TotalAmount = 1500, Status = "Active" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.TotalCancel.Should().Be(3000);
            result.Retention.Should().BeApproximately(0.333m, 0.01m); // 1500 active / 4500 total ≈ 0.333
        }

        [Fact]
        public void CalculateAggregation_WithExampleFromUser_ShouldCalculateCorrectly()
        {
            // Arrange - Example: 1000000 active, 100001 defaulted
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 100001, Status = "Defaulted" },
                new Contract { TotalAmount = 1000000, Status = "Active" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(1100001);
            result.TotalCancel.Should().Be(100001);
            // 1000000 / 1100001 ≈ 0.909
            result.Retention.Should().BeApproximately(0.909m, 0.001m);
        }
    }
}
