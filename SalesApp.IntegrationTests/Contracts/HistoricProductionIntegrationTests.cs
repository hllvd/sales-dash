using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    public class HistoricProductionIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public HistoricProductionIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetHistoricProduction_AsAdmin_ReturnsMonthlyData()
        {
            // Arrange
            var (client, userId) = await _factory.CreateAuthenticatedClientAsync("admin");
            
            // Create test contracts in different months
            var contracts = new[]
            {
                new { ContractNumber = "HP-001", TotalAmount = 100000m, SaleStartDate = new DateTime(2024, 7, 1) },
                new { ContractNumber = "HP-002", TotalAmount = 150000m, SaleStartDate = new DateTime(2024, 7, 15) },
                new { ContractNumber = "HP-003", TotalAmount = 200000m, SaleStartDate = new DateTime(2024, 8, 1) }
            };

            foreach (var contract in contracts)
            {
                var createRequest = new
                {
                    contractNumber = contract.ContractNumber,
                    totalAmount = contract.TotalAmount,
                    status = "Active",
                    contractStartDate = contract.SaleStartDate
                };
                await client.PostAsJsonAsync("/api/contracts", createRequest);
            }

            // Act
            var response = await client.GetAsync("/api/contracts/aggregation/historic-production");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricProductionResponse>>();
            
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.MonthlyData.Should().HaveCountGreaterOrEqualTo(2);
            
            var july = result.Data.MonthlyData.FirstOrDefault(m => m.Period == "2024-07");
            july.Should().NotBeNull();
            july!.TotalProduction.Should().Be(250000);
            july.ContractCount.Should().Be(2);
            
            var august = result.Data.MonthlyData.FirstOrDefault(m => m.Period == "2024-08");
            august.Should().NotBeNull();
            august!.TotalProduction.Should().Be(200000);
            august.ContractCount.Should().Be(1);
        }

        [Fact]
        public async Task GetHistoricProduction_WithDateFilter_ReturnsFilteredData()
        {
            // Arrange
            var (client, userId) = await _factory.CreateAuthenticatedClientAsync("admin");
            
            // Create contracts
            var contracts = new[]
            {
                new { ContractNumber = "HP-F1", TotalAmount = 100000m, SaleStartDate = new DateTime(2024, 6, 1) },
                new { ContractNumber = "HP-F2", TotalAmount = 150000m, SaleStartDate = new DateTime(2024, 7, 1) },
                new { ContractNumber = "HP-F3", TotalAmount = 200000m, SaleStartDate = new DateTime(2024, 8, 1) }
            };

            foreach (var contract in contracts)
            {
                var createRequest = new
                {
                    contractNumber = contract.ContractNumber,
                    totalAmount = contract.TotalAmount,
                    status = "Active",
                    contractStartDate = contract.SaleStartDate
                };
                await client.PostAsJsonAsync("/api/contracts", createRequest);
            }

            // Act - Filter for July only
            var response = await client.GetAsync("/api/contracts/aggregation/historic-production?startDate=2024-07-01&endDate=2024-07-31");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricProductionResponse>>();
            
            result.Should().NotBeNull();
            result!.Data.MonthlyData.Should().Contain(m => m.Period == "2024-07");
            result.Data.MonthlyData.Should().NotContain(m => m.Period == "2024-06");
            result.Data.MonthlyData.Should().NotContain(m => m.Period == "2024-08");
        }

        [Fact]
        public async Task GetHistoricProduction_WithUserId_ReturnsUserSpecificData()
        {
            // Arrange
            var (client, userId) = await _factory.CreateAuthenticatedClientAsync("admin");
            var (otherClient, otherUserId) = await _factory.CreateAuthenticatedClientAsync("user");
            
            // Create contracts for specific user
            var createRequest = new
            {
                contractNumber = "HP-U1",
                userId = userId.ToString(),
                totalAmount = 100000m,
                status = "Active",
                contractStartDate = new DateTime(2024, 7, 1)
            };
            await client.PostAsJsonAsync("/api/contracts", createRequest);

            // Act
            var response = await client.GetAsync($"/api/contracts/aggregation/historic-production?userId={userId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricProductionResponse>>();
            
            result.Should().NotBeNull();
            result!.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetHistoricProduction_NoContracts_ReturnsEmptyData()
        {
            // Arrange
            var (client, userId) = await _factory.CreateAuthenticatedClientAsync("admin");

            // Act - Use future date range with no contracts
            var response = await client.GetAsync("/api/contracts/aggregation/historic-production?startDate=2099-01-01&endDate=2099-12-31");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<HistoricProductionResponse>>();
            
            result.Should().NotBeNull();
            result!.Data.MonthlyData.Should().BeEmpty();
            result.Data.TotalProduction.Should().Be(0);
            result.Data.TotalContracts.Should().Be(0);
        }
    }
}
