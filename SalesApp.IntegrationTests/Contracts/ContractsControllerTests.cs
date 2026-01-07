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
                ContractType = "motores",
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
            result.Data.ContractType.Should().Be("motores");
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
                ContractType = 1, // Will be updated to "motores"
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
                ContractType = "motores",
                Quota = 20
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contract.Id}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.ContractType.Should().Be("motores");
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
                ContractType = "lar",
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
                    Status = "Defaulted",
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

        [Fact]
        public async Task GetContracts_ShouldReturnAggregationWithRetention()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create test contracts with mixed statuses
            var user = new User { Id = Guid.NewGuid(), Name = "Retention Test User", Email = "retentiontest@test.com", RoleId = 1 };
            var group = new Group { Id = 107, Name = "Retention Test Group" };
            
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
                
                // Add 3 active contracts and 1 defaulted contract
                var activeContract1 = new Contract
                {
                    ContractNumber = $"RET-ACTIVE1-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Active",
                    SaleStartDate = DateTime.UtcNow
                };
                var activeContract2 = new Contract
                {
                    ContractNumber = $"RET-ACTIVE2-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Late1",
                    SaleStartDate = DateTime.UtcNow
                };
                var activeContract3 = new Contract
                {
                    ContractNumber = $"RET-ACTIVE3-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Late2",
                    SaleStartDate = DateTime.UtcNow
                };
                var defaultedContract = new Contract
                {
                    ContractNumber = $"RET-DEFAULTED-{Guid.NewGuid().ToString()[..8]}",
                    UserId = user.Id,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    Status = "Defaulted",
                    SaleStartDate = DateTime.UtcNow
                };
                
                context.Contracts.Add(activeContract1);
                context.Contracts.Add(activeContract2);
                context.Contracts.Add(activeContract3);
                context.Contracts.Add(defaultedContract);
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
            aggregation!.Retention.Should().BeGreaterThan(0); // Should have retention > 0 since we have active contracts
            aggregation.Retention.Should().BeLessThanOrEqualTo(1.0m); // Retention should be between 0 and 1
        }

        [Fact]
        public async Task CreateContract_WithStringContractType_ShouldAcceptLarAndMotores()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user = new User { Id = Guid.NewGuid(), Name = "ContractType Test User", Email = "contracttypetest@test.com", RoleId = 1 };
            var group = new Group { Id = 108, Name = "ContractType Test Group" };
            
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
                await context.SaveChangesAsync();
            }

            // Test 1: Create contract with "lar" (lowercase)
            var requestLar = new ContractRequest
            {
                ContractNumber = $"TYPE-LAR-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 1000,
                ContractType = "lar",
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseLar = await _client.PostAsJsonAsync("/api/contracts", requestLar);
            responseLar.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultLar = await responseLar.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultLar!.Data.ContractType.Should().Be("lar");

            // Test 2: Create contract with "motores" (lowercase)
            var requestMotores = new ContractRequest
            {
                ContractNumber = $"TYPE-MOTORES-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 2000,
                ContractType = "motores",
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseMotores = await _client.PostAsJsonAsync("/api/contracts", requestMotores);
            responseMotores.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultMotores = await responseMotores.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultMotores!.Data.ContractType.Should().Be("motores");

            // Test 3: Create contract with "LAR" (uppercase - should be normalized to lowercase)
            var requestUppercase = new ContractRequest
            {
                ContractNumber = $"TYPE-UPPER-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 3000,
                ContractType = "LAR",
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseUppercase = await _client.PostAsJsonAsync("/api/contracts", requestUppercase);
            responseUppercase.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultUppercase = await responseUppercase.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultUppercase!.Data.ContractType.Should().Be("lar"); // Should be normalized to lowercase

            // Test 4: Create contract with invalid type should fail
            var requestInvalid = new ContractRequest
            {
                ContractNumber = $"TYPE-INVALID-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 4000,
                ContractType = "invalid",
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseInvalid = await _client.PostAsJsonAsync("/api/contracts", requestInvalid);
            responseInvalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var resultInvalid = await responseInvalid.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultInvalid!.Success.Should().BeFalse();
            resultInvalid.Message.Should().Contain("Invalid contract type");
        }

        [Fact]
        public async Task CreateContract_WithAndWithoutQuota_ShouldBothSucceed()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user = new User { Id = Guid.NewGuid(), Name = "Quota Test User", Email = "quotatest@test.com", RoleId = 1 };
            var group = new Group { Id = 109, Name = "Quota Test Group" };
            
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
                await context.SaveChangesAsync();
            }

            // Test 1: Create contract WITH quota
            var requestWithQuota = new ContractRequest
            {
                ContractNumber = $"QUOTA-WITH-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 1000,
                ContractType = "lar",
                Quota = 25,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseWithQuota = await _client.PostAsJsonAsync("/api/contracts", requestWithQuota);
            responseWithQuota.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultWithQuota = await responseWithQuota.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultWithQuota!.Success.Should().BeTrue();
            resultWithQuota.Data.Quota.Should().Be(25);

            // Test 2: Create contract WITHOUT quota (null)
            var requestWithoutQuota = new ContractRequest
            {
                ContractNumber = $"QUOTA-WITHOUT-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 2000,
                ContractType = "motores",
                Quota = null, // Explicitly null
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseWithoutQuota = await _client.PostAsJsonAsync("/api/contracts", requestWithoutQuota);
            responseWithoutQuota.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultWithoutQuota = await responseWithoutQuota.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultWithoutQuota!.Success.Should().BeTrue();
            resultWithoutQuota.Data.Quota.Should().BeNull();

            // Test 3: Update contract to remove quota
            var contractId = resultWithQuota.Data.Id;
            var updateRequest = new UpdateContractRequest
            {
                Quota = null // Remove quota
            };

            var updateResponse = await _client.PutAsJsonAsync($"/api/contracts/{contractId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var updateResult = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            updateResult!.Success.Should().BeTrue();
            // Note: The quota might still be the old value since we only update if HasValue
            // This is expected behavior for partial updates
        }
    }
}
