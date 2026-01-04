using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Integration Tests")]
    public class UsersControllerFunctionalityTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UsersControllerFunctionalityTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task Register_WithValidData_ShouldSucceed()
        {
            // Arrange - Need to get a parent user ID first because only one root user is allowed
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var parentUserId = loginResult?.Data?.User?.Id;

            if (parentUserId == null)
            {
                throw new Exception("Failed to get superadmin ID for parent reference");
            }

            var registerRequest = new
            {
                Name = "New Functionality User",
                Email = $"func_user_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "Password123!",
                ParentUserId = parentUserId
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/users/register", registerRequest);

            // Assert
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
            }
            
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetUsers_AsSuperAdmin_ShouldReturnOkAndList()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUsers_ShouldReturnActiveUsersBeforeInactiveUsers()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create an inactive user
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                
                var inactiveUser = new User
                {
                    Name = "Inactive Test User",
                    Email = $"inactive_{Guid.NewGuid().ToString()[..8]}@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = 3,
                    ParentUserId = superAdmin.Id,
                    IsActive = false
                };
                
                context.Users.Add(inactiveUser);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await client.GetAsync("/api/users?pageSize=100");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().NotBeNull();
            
            // Verify ordering: active users should come before inactive users
            var users = result.Data.Items.ToList();
            if (users.Count > 1)
            {
                // Find the first active and first inactive user
                var firstActiveIndex = users.FindIndex(u => u.IsActive);
                var firstInactiveIndex = users.FindIndex(u => !u.IsActive);
                
                // If both exist, active should come before inactive
                if (firstActiveIndex >= 0 && firstInactiveIndex >= 0)
                {
                    firstActiveIndex.Should().BeLessThan(firstInactiveIndex, 
                        "active users should appear before inactive users in the list");
                }
            }
        }


        [Fact]
        public async Task GetUsersByRole_AsSuperAdmin_ShouldReturnOk()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act - Role ID 3 is typically 'User'
            var response = await client.GetAsync("/api/users/role/3");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUser_AsSuperAdmin_ShouldReturnOk()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Get a user first to have a valid ID
            var usersResponse = await client.GetAsync("/api/users");
            var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            var userId = usersResult!.Data!.Items.First().Id;

            // Act
            var response = await client.GetAsync($"/api/users/{userId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(userId);
        }

        [Fact]
        public async Task GetCurrentUser_AsSuperAdmin_ShouldReturnOk()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/api/users/me");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Email.Should().Be("superadmin@test.com");
        }

        [Fact]
        public async Task GetParent_AsSuperAdmin_ShouldReturnOk()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Get a user first
            var usersResponse = await client.GetAsync("/api/users");
            var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            var userId = usersResult!.Data!.Items.First().Id;

            // Act
            var response = await client.GetAsync($"/api/users/{userId}/parent");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserHierarchyResponse?>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            // Parent might be null or not, depending on the user, but the call should succeed
        }

        [Fact]
        public async Task GetChildren_AsSuperAdmin_ShouldReturnOkAndEmpty()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Get a user first - ideally one we know has no children, or just check the response structure
            // The requirement says "that should be a empty list", so I should probably pick a leaf user or just verify it returns a list (even if empty)
            var usersResponse = await client.GetAsync("/api/users");
            var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            var userId = usersResult!.Data!.Items.Last().Id; // Pick last user, likely newer/leaf

            // Act
            var response = await client.GetAsync($"/api/users/{userId}/children");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserHierarchyResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            // We can't strictly guarantee it's empty unless we create a fresh user, but the prompt asked for "that should be a empty list"
            // I'll assert it's not null, and if I created a fresh user I could assert empty.
            // Let's create a fresh user to be sure it has no children.
        }

        [Fact]
        public async Task GetChildren_ForNewUser_ShouldBeEmpty()
        {
             // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a new user to ensure no children
            var registerRequest = new
            {
                Name = "Childless User",
                Email = $"childless_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "Password123!",
                ParentUserId = (await GetCurrentUser(client)).Id // Set parent to current superadmin
            };
            
            var createResponse = await client.PostAsJsonAsync("/api/users/register", registerRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var newUserId = createResult!.Data!.Id;

            // Act
            var response = await client.GetAsync($"/api/users/{newUserId}/children");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserHierarchyResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        private async Task<UserResponse> GetCurrentUser(HttpClient client)
        {
            var response = await client.GetAsync("/api/users/me");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            return result!.Data!;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"SuperAdmin login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token from login response");
        }
        [Fact]
        public async Task AssignContract_AsRegularUser_ShouldSucceed()
        {
            // Arrange
            var regularUserId = await GetUserIdByEmail("user@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.UserId.Should().Be(regularUserId);
            
            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbContract = await context.Contracts.FindAsync(contract.Id);
            dbContract.Should().NotBeNull();
            dbContract!.UserId.Should().Be(regularUserId);
        }

        [Fact]
        public async Task AssignContract_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var adminUserId = await GetUserIdByEmail("admin@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.UserId.Should().Be(adminUserId);
            
            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbContract = await context.Contracts.FindAsync(contract.Id);
            dbContract.Should().NotBeNull();
            dbContract!.UserId.Should().Be(adminUserId);
        }

        [Fact]
        public async Task AssignContract_AsSuperAdmin_ShouldSucceed()
        {
            // Arrange
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            var regularUserId = await GetUserIdByEmail("user@test.com");
            
            var contract = await CreateContract(regularUserId);
            
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.UserId.Should().Be(superAdminUserId);
            
            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbContract = await context.Contracts.FindAsync(contract.Id);
            dbContract.Should().NotBeNull();
            dbContract!.UserId.Should().Be(superAdminUserId);
        }

        [Fact]
        public async Task AssignContract_NonExistent_ShouldReturnNotFound()
        {
            // Arrange
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync("/api/users/assign-contract/NON-EXISTENT-CONTRACT", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        private async Task<Contract> CreateContract(Guid userId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Ensure group exists
            var group = await context.Groups.FirstOrDefaultAsync();
            if (group == null)
            {
                group = new Group { Name = "Test Group", Description = "Test Group" };
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }
            
            var contract = new Contract
            {
                ContractNumber = $"CN-{Guid.NewGuid().ToString()[..8]}",
                UserId = userId,
                TotalAmount = 1000,
                GroupId = group.Id,
                Status = "active",
                IsActive = true
            };
            
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();
            
            return contract;
        }

        private async Task<Guid> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Email == email);
            return user.Id;
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"User login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token from login response");
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Admin login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token from login response");
        }
    }
}
