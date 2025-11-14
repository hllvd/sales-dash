using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests
{
    public class RolesControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public RolesControllerIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetRoles_WithoutAuth_ShouldReturnUnauthorized()
        {
            var response = await _client.GetAsync("/api/roles");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateRole_WithValidData_ShouldCreateRole()
        {
            // Arrange
            await SeedDatabase();
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new RoleRequest
            {
                Name = "manager",
                Description = "Manager role",
                Level = 3,
                Permissions = "{\"canEdit\": true}"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/roles", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Name.Should().Be("manager");
        }

        [Fact]
        public async Task GetRoles_WithAuth_ShouldReturnRoles()
        {
            // Arrange
            await SeedDatabase();
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/roles");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<RoleResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task UpdateRole_WithValidData_ShouldUpdateRole()
        {
            // Arrange
            await SeedDatabase();
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var updateRequest = new UpdateRoleRequest
            {
                Name = "updated-admin",
                Description = "Updated admin role"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/roles/2", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Name.Should().Be("updated-admin");
        }

        [Fact]
        public async Task DeleteRole_WithValidId_ShouldDeleteRole()
        {
            // Arrange
            await SeedDatabase();
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.DeleteAsync("/api/roles/3");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        private async Task SeedDatabase()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            context.Database.EnsureCreated();

            // Seed roles
            var roles = new[]
            {
                new Role { Id = 1, Name = "superadmin", Description = "Super Admin", Level = 1, IsActive = true },
                new Role { Id = 2, Name = "admin", Description = "Admin", Level = 2, IsActive = true },
                new Role { Id = 3, Name = "user", Description = "User", Level = 3, IsActive = true }
            };
            context.Roles.AddRange(roles);

            // Seed admin user
            var adminUser = new User
            {
                Name = "Admin User",
                Email = "admin@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                RoleId = 2,
                IsActive = true
            };
            context.Users.Add(adminUser);

            await context.SaveChangesAsync();
        }
    }
}