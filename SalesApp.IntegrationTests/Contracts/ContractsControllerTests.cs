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
    public class ContractsControllerTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ContractsControllerTests(TestWebApplicationFactory factory)
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
        public async Task CreateContract_ShouldSaveContractTypeAndQuota()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a user and group first
            var user = new User { Id = Guid.NewGuid(), Name = "Contract Test User", Email = "contracttest@test.com", RoleId = 1 };
            var group = new Group { Id = 101, Name = "Contract Test Group" };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
                // Check if user/group already exists to avoid conflicts if tests run in parallel or reuse db
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                await context.SaveChangesAsync();
            }

            var request = new ContractRequest
            {
                ContractNumber = "CTR-TEST-001",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 5000,
                ContractType = 1,
                Quota = 10,
                CustomerName = "Jane Doe",
                Status = "active",
                ContractStartDate = DateTime.UtcNow
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/contracts", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.ContractType.Should().Be(1);
            result.Data.Quota.Should().Be(10);
            result.Data.CustomerName.Should().Be("Jane Doe");
        }

        [Fact]
        public async Task UpdateContract_ShouldUpdateContractTypeAndQuota()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a user, group and contract
            var user = new User { Id = Guid.NewGuid(), Name = "Contract Test User 2", Email = "contracttest2@test.com", RoleId = 1 };
            var group = new Group { Id = 102, Name = "Contract Test Group 2" };
            var contract = new Contract
            {
                ContractNumber = "CTR-TEST-002",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 3000,
                ContractType = 1,
                Quota = 5,
                Status = "active",
                SaleStartDate = DateTime.UtcNow
            };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
                // Check if user/group already exists
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                // Check if contract exists
                if (!context.Contracts.Any(c => c.ContractNumber == contract.ContractNumber))
                {
                    context.Contracts.Add(contract);
                }
                await context.SaveChangesAsync();
                
                // If contract already existed (from previous run), get its ID
                if (contract.Id == 0)
                {
                    contract = context.Contracts.First(c => c.ContractNumber == "CTR-TEST-002");
                }
            }

            var updateRequest = new UpdateContractRequest
            {
                ContractType = 2,
                Quota = 20
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contract.Id}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.ContractType.Should().Be(2);
            result.Data.Quota.Should().Be(20);
        }
    }
}
