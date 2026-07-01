using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.Models;
using SalesApp.ReportFilters.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    /// <summary>
    /// Unit tests for ReportRetentionCalculator.
    /// Pure function tests — no database, no mocks, zero side effects.
    /// </summary>
    public class ReportRetentionCalculatorTests
    {
        [Fact]
        public void CalculateOverallRetention_WithEmptyList_ShouldReturnZero()
        {
            // Arrange
            var contracts = new List<Contract>();

            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(contracts, "standard");

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public void CalculateOverallRetention_WithNullList_ShouldReturnZero()
        {
            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(null!, "standard");

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public void CalculateOverallRetention_WithZeroTotalAmount_ShouldReturnZero()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 0m, ContractStatus = new ContractStatusEntity { Name = "Active" } }
            };

            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(contracts, "standard");

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public void CalculateOverallRetention_WithStandardRetentionType_ShouldIgnoreDefaultedOnly()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000m, ContractStatus = new ContractStatusEntity { Name = "Active" } },
                new Contract { TotalAmount = 500m, ContractStatus = new ContractStatusEntity { Name = "Late1" } },
                new Contract { TotalAmount = 300m, ContractStatus = new ContractStatusEntity { Name = "Late2" } },
                new Contract { TotalAmount = 200m, ContractStatus = new ContractStatusEntity { Name = "Defaulted" } }
            }; // Total = 2000, Active (non-Defaulted) = 1800

            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(contracts, "standard");

            // Assert
            result.Should().Be(0.9m); // 1800 / 2000 = 0.90 (90%)
        }

        [Fact]
        public void CalculateOverallRetention_WithStrictRetentionType_ShouldIgnoreDefaultedAndAllLateStatuses()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000m, ContractStatus = new ContractStatusEntity { Name = "Active" } },
                new Contract { TotalAmount = 500m, ContractStatus = new ContractStatusEntity { Name = "Late1" } },
                new Contract { TotalAmount = 300m, ContractStatus = new ContractStatusEntity { Name = "Late2" } },
                new Contract { TotalAmount = 200m, ContractStatus = new ContractStatusEntity { Name = "Defaulted" } }
            }; // Total = 2000, Active (Active only, excluding Late1/Late2/Defaulted) = 1000

            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(contracts, "strict");

            // Assert
            result.Should().Be(0.5m); // 1000 / 2000 = 0.50 (50%)
        }

        [Fact]
        public void CalculateOverallRetention_WithNullRetentionType_ShouldDefaultToStandard()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000m, ContractStatus = new ContractStatusEntity { Name = "Active" } },
                new Contract { TotalAmount = 500m, ContractStatus = new ContractStatusEntity { Name = "Late1" } },
                new Contract { TotalAmount = 200m, ContractStatus = new ContractStatusEntity { Name = "Defaulted" } }
            }; // Total = 1700, Standard Active = 1500

            // Act
            var result = ReportRetentionCalculator.CalculateOverallRetention(contracts, null);

            // Assert
            result.Should().Be(1500m / 1700m);
        }

        [Fact]
        public void CalculateOverallRetention_WithTransferredStatus_ShouldTreatAsActive()
        {
            // Arrange
            var contracts = new List<Contract>
            {
                new Contract { TotalAmount = 1000m, ContractStatus = new ContractStatusEntity { Name = "Active" } },
                new Contract { TotalAmount = 500m, ContractStatus = new ContractStatusEntity { Name = "Transferred" } },
                // Standard active should be 1500, Strict active should be 1500
                new Contract { TotalAmount = 200m, ContractStatus = new ContractStatusEntity { Name = "Defaulted" } }
            }; // Total = 1700

            // Act & Assert
            ReportRetentionCalculator.CalculateOverallRetention(contracts, "standard").Should().Be(1500m / 1700m);
            ReportRetentionCalculator.CalculateOverallRetention(contracts, "strict").Should().Be(1500m / 1700m);
        }
    }
}
