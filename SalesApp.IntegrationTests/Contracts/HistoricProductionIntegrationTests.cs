using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
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
            _client = factory.Client;
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "superadmin@test.com",
                password = "superadmin123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task GetHistoricProduction_AsAdmin_ReturnsMonthlyData()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Create test user and group
            var userId = Guid.NewGuid();
            var groupId = 201;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "HP Test User", Email = "hptest@test.com", RoleId = 1 };
                var group = new Group { Id = groupId, Name = "HP Test Group" };
                
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }
            
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
                    userId = userId.ToString(),
                    groupId = groupId,
                    totalAmount = contract.TotalAmount,
                    status = "Active",
                    contractStartDate = contract.SaleStartDate
                };
                await _client.PostAsJsonAsync("/api/contracts", createRequest);
            }

            // Act
            var response = await _client.GetAsync("/api/contracts/aggregation/historic-production");

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
        public async Task GetHistoricProduction_NoContracts_ReturnsEmptyData()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act - Use future date range with no contracts
            var response = await _client.GetAsync("/api/contracts/aggregation/historic-production?startDate=2099-01-01&endDate=2099-12-31");

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
