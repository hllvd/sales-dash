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
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Active" },
                new Contract { TotalAmount = 2000, Status = "Late1" },
                new Contract { TotalAmount = 1500, Status = "Late2" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(4500);
            result.TotalCancel.Should().Be(0);
            result.Retention.Should().Be(1.0m);
        }

        [Fact]
        public void CalculateAggregation_WithAllDefaultedContracts_ShouldReturnRetentionOf0()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Defaulted" },
                new Contract { TotalAmount = 2000, Status = "Defaulted" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(3000);
            result.TotalCancel.Should().Be(3000);
            result.Retention.Should().Be(0.0m);
        }

        [Fact]
        public void CalculateAggregation_WithMixedStatuses_ShouldCalculateCorrectRetention()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000, Status = "Active" },
                new Contract { TotalAmount = 2000, Status = "Defaulted" },
                new Contract { TotalAmount = 1500, Status = "Late1" },
                new Contract { TotalAmount = 500, Status = "Defaulted" }
            };

            // Act
            var result = _service.CalculateAggregation(contracts);

            // Assert
            result.Total.Should().Be(5000);
            result.TotalCancel.Should().Be(2500); // 2000 + 500
            result.Retention.Should().Be(0.5m); // 2 out of 4 contracts are non-defaulted
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
            // Arrange
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
            result.Retention.Should().BeApproximately(0.333m, 0.01m); // 1 out of 3
        }
    }
}
