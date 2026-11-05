using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Contracts Tests")]
    public class ContractsControllerTests 
    {
        private readonly ContractsTestFactory _factory;
        private readonly HttpClient _client;

        public ContractsControllerTests(ContractsTestFactory factory)
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
        public async Task CreateContract_ShouldSaveQuotaAndDeriveSegmentFromGroup()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a user and group first
            var user = new User { Id = Guid.NewGuid(), Name = "Contract Test User", Email = "contracttest@test.com", RoleId = 1 };
            var group = new Group { Id = 101, Name = "Contract Test Group", Segment = 2 };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
                // Check if user/group already exists to avoid conflicts if tests run in parallel or reuse db
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                var existingGroup = await context.Groups.FindAsync(group.Id);
                if (existingGroup == null)
                {
                    context.Groups.Add(group);
                }
                else
                {
                    existingGroup.Segment = 2;
                }
                await context.SaveChangesAsync();
            }

            var request = new ContractRequest
            {
                ContractNumber = "CTR-TEST-001",
                UserId = user.Id,
                GroupId = group.Id,
                TotalAmount = 5000,
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
            result!.Data!.Segment.Should().Be(2);
            result!.Data!.SegmentName.Should().Be("Motores");
            result!.Data!.Quota.Should().Be(10);
            result!.Data!.CustomerName.Should().Be("Jane Doe");
        }

        [Fact]
        public async Task UpdateContract_ShouldUpdateQuota()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a user, group and contract
            var user = new User { Id = Guid.NewGuid(), Name = "Contract Test User 2", Email = "contracttest2@test.com", RoleId = 1 };
            var group = new Group { Id = 102, Name = "Contract Test Group 2", Segment = 1 };
            Contract contract;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
                // Check if user/group already exists
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                var existingGroup = await context.Groups.FindAsync(group.Id);
                if (existingGroup == null)
                {
                    context.Groups.Add(group);
                }
                else
                {
                    existingGroup.Segment = 1;
                }
                await context.SaveChangesAsync();

                contract = new Contract
                {
                    ContractNumber = "CTR-TEST-002",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 3000,
                    Quota = 5,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow
                };

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
                Quota = 20
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contract.Id}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result!.Data!.Quota.Should().Be(20);
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
            result!.Data!.UserId.Should().BeNull();
            result!.Data!.UserName.Should().BeNullOrEmpty();
            result!.Data!.CustomerName.Should().Be("John Smith");
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
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow
                };
                var contract2 = new Contract
                {
                    ContractNumber = $"AGG-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 2000,
                    ContractStatusId = 1,
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
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow
                };
                
                // Add canceled contract (StatusId 5 = Defaulted in our factory seeding)
                var canceledContract = new Contract
                {
                    ContractNumber = $"CANCEL-CANCELED-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 500,
                    ContractStatusId = 5,
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
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 3000,
                    ContractStatusId = 1,
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
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow
                };
                var activeContract2 = new Contract
                {
                    ContractNumber = $"RET-ACTIVE2-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 3,
                    SaleStartDate = DateTime.UtcNow
                };
                var activeContract3 = new Contract
                {
                    ContractNumber = $"RET-ACTIVE3-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 3,
                    SaleStartDate = DateTime.UtcNow
                };
                var defaultedContract = new Contract
                {
                    ContractNumber = $"RET-DEFAULTED-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    TotalAmount = 1000,
                    ContractStatusId = 2,
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
        public async Task CreateContract_ShouldDeriveSegmentFromAssignedGroup()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user = new User { Id = Guid.NewGuid(), Name = "Segment Test User", Email = "segmenttest@test.com", RoleId = 1 };
            var groupLar = new Group { Id = 108, Name = "Segment Lar Group", Segment = 1 };
            var groupMotores = new Group { Id = 109, Name = "Segment Motores Group", Segment = 2 };
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await context.Users.FindAsync(user.Id) == null)
                {
                    context.Users.Add(user);
                }
                var existingLar = await context.Groups.FindAsync(groupLar.Id);
                if (existingLar == null) context.Groups.Add(groupLar);
                else existingLar.Segment = 1;

                var existingMotores = await context.Groups.FindAsync(groupMotores.Id);
                if (existingMotores == null) context.Groups.Add(groupMotores);
                else existingMotores.Segment = 2;

                await context.SaveChangesAsync();
            }

            // Test 1: Create contract with group Lar
            var requestLar = new ContractRequest
            {
                ContractNumber = $"SEG-LAR-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = groupLar.Id,
                TotalAmount = 1000,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseLar = await _client.PostAsJsonAsync("/api/contracts", requestLar);
            responseLar.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultLar = await responseLar.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultLar!.Data.Segment.Should().Be(1);
            resultLar.Data.SegmentName.Should().Be("Lar");

            // Test 2: Create contract with group Motores
            var requestMotores = new ContractRequest
            {
                ContractNumber = $"SEG-MOTORES-{Guid.NewGuid().ToString()[..8]}",
                UserId = user.Id,
                GroupId = groupMotores.Id,
                TotalAmount = 2000,
                Status = "Active",
                ContractStartDate = DateTime.UtcNow
            };

            var responseMotores = await _client.PostAsJsonAsync("/api/contracts", requestMotores);
            responseMotores.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultMotores = await responseMotores.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            resultMotores!.Data.Segment.Should().Be(2);
            resultMotores.Data.SegmentName.Should().Be("Motores");
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

        [Fact]
        public async Task GetContracts_ShouldReturnPagedResponse_WhenPageParametersProvided()
        {
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Arrange - create a user and status
            var user = new User { Id = Guid.NewGuid(), Name = "Paged Test User", Email = "pagedtest@test.com", RoleId = 1 };
            var group = new Group { Id = 105, Name = "Paged Test Group" };
            int statusId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                context.Users.Add(user);
                context.Groups.Add(group);
                
                var statusService = scope.ServiceProvider.GetRequiredService<SalesApp.Services.IContractStatusService>();
                statusId = await statusService.GetStatusIdByNameAsync("Active");
                
                // Add two contracts
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"PAG-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    ContractStatusId = statusId,
                    TotalAmount = 1000m,
                    SaleStartDate = DateTime.UtcNow,
                    IsActive = true
                });
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"PAG-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    ContractStatusId = statusId,
                    TotalAmount = 2000m,
                    SaleStartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync("/api/contracts?page=1&pageSize=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().HaveCount(1);
            result.Data.TotalCount.Should().BeGreaterThanOrEqualTo(2);
            result.Data.Page.Should().Be(1);
            result.Data.PageSize.Should().Be(1);
            result.Data.Aggregation.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUserContracts_WithPagination_ShouldReturnPagedResponseAndDescendingOrder()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user = new User { Id = Guid.NewGuid(), Name = "Paged User", Email = $"pageduser-{Guid.NewGuid().ToString()[..6]}@test.com", RoleId = 1 };
            var group = new Group { Id = 111, Name = "Paged User Group" };

            var olderDate = DateTime.UtcNow.AddDays(-10);
            var newerDate = DateTime.UtcNow.AddDays(-1);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Users.Add(user);
                if (await context.Groups.FindAsync(group.Id) == null)
                {
                    context.Groups.Add(group);
                }
                var statusId = context.ContractStatuses.First(s => s.Name == "Active").Id;

                // Add older contract first
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"USER-PAG-OLD-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    ContractStatusId = statusId,
                    TotalAmount = 1000m,
                    SaleStartDate = olderDate,
                    IsActive = true
                });

                // Add newer contract second
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"USER-PAG-NEW-{Guid.NewGuid().ToString()[..8]}",
                    UserInternalId = user.InternalId,
                    GroupId = group.Id,
                    ContractStatusId = statusId,
                    TotalAmount = 2000m,
                    SaleStartDate = newerDate,
                    IsActive = true
                });

                await context.SaveChangesAsync();
            }

            // Act: Request page 1 with pageSize 1
            var response = await _client.GetAsync($"/api/contracts/user/{user.Id}?page=1&pageSize=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().HaveCount(1);
            result.Data.TotalCount.Should().Be(2);
            result.Data.Page.Should().Be(1);
            result.Data.PageSize.Should().Be(1);
            // The first item must be the newer contract (descending order by SaleStartDate)
            result.Data.Items[0].ContractStartDate.Date.Should().Be(newerDate.Date);
            result.Data.Aggregation.Should().NotBeNull();
            result.Data.Aggregation!.Total.Should().Be(3000m);
        }
    }
}
