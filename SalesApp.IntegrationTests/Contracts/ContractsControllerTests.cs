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
                Status = "Active",
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
                Status = "Active",
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
                ContractType = 1, // Motors
                Quota = 20
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contract.Id}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.ContractType.Should().Be(1); // Motors
            result.Data.Quota.Should().Be(20);
        }

        [Fact]
        public async Task CreateContract_WithoutUserId_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a group
            var group = new Group { Id = 103, Name = "Contract Test Group 3" };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                await context.SaveChangesAsync();
            }

            var request = new ContractRequest
            {
                ContractNumber = "CTR-TEST-003",
                UserId = null, // No user assigned
                GroupId = group.Id,
                TotalAmount = 4000,
                ContractType = 1,
                Quota = 15,
                CustomerName = "John Smith",
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/contracts", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.UserId.Should().BeNull();
            result.Data.UserName.Should().BeNullOrEmpty();
            result.Data.CustomerName.Should().Be("John Smith");
        }

        [Fact]
        public async Task GetContracts_ShouldReturnAggregationWithTotal()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create test contracts
            var user = new User { Id = Guid.NewGuid(), Name = "Agg Test User", Email = "aggtest@test.com", RoleId = 1 };
            var group = new Group { Id = 104, Name = "Agg Test Group" };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                
                // Add contracts with different amounts
                var contract1 = new Contract
                {
                    ContractNumber = $"AGG-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow
                };
                var contract2 = new Contract
                {
                    ContractNumber = $"AGG-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 2000,
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow
                };
                
                context.Contracts.Add(contract1);
                context.Contracts.Add(contract2);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/contracts?userId={user.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Aggregation.Should().NotBeNull();
            
            var aggregationJson = System.Text.Json.JsonSerializer.Serialize(result.Aggregation);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aggregation = System.Text.Json.JsonSerializer.Deserialize<ContractAggregation>(aggregationJson, options);
            aggregation.Should().NotBeNull();
            aggregation!.Total.Should().BeGreaterOrEqualTo(3000); // At least our test contracts
        }

        [Fact]
        public async Task GetContracts_ShouldReturnAggregationWithTotalCancel()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create test contracts with canceled status
            var user = new User { Id = Guid.NewGuid(), Name = "Cancel Test User", Email = "canceltest@test.com", RoleId = 1 };
            var group = new Group { Id = 105, Name = "Cancel Test Group" };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                
                // Add active contract
                var activeContract = new Contract
                {
                    ContractNumber = $"CANCEL-ACTIVE-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow
                };
                
                // Add canceled contract
                var canceledContract = new Contract
                {
                    ContractNumber = $"CANCEL-CANCELED-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 500,
                    Status = "Canceled",
                    SaleStartDate = DateTime.UtcNow
                };
                
                context.Contracts.Add(activeContract);
                context.Contracts.Add(canceledContract);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/contracts?userId={user.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Aggregation.Should().NotBeNull();
            
            var aggregationJson = System.Text.Json.JsonSerializer.Serialize(result.Aggregation);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aggregation = System.Text.Json.JsonSerializer.Deserialize<ContractAggregation>(aggregationJson, options);
            aggregation.Should().NotBeNull();
            aggregation!.Total.Should().BeGreaterOrEqualTo(1500); // Active + Canceled
            aggregation.TotalCancel.Should().BeGreaterOrEqualTo(500); // Only canceled
        }

        [Fact]
        public async Task GetUserContracts_ShouldReturnAggregation()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create test user and contracts
            var user = new User { Id = Guid.NewGuid(), Name = "User Agg Test", Email = "useragg@test.com", RoleId = 1 };
            var group = new Group { Id = 106, Name = "User Agg Group" };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                
                var contract = new Contract
                {
                    ContractNumber = $"USER-AGG-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 3000,
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow
                };
                
                context.Contracts.Add(contract);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/contracts/user/{user.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Aggregation.Should().NotBeNull();
            
            var aggregationJson = System.Text.Json.JsonSerializer.Serialize(result.Aggregation);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aggregation = System.Text.Json.JsonSerializer.Deserialize<ContractAggregation>(aggregationJson, options);
            aggregation.Should().NotBeNull();
            aggregation!.Total.Should().BeGreaterOrEqualTo(3000);
        }
    }
}
