using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
    public class AdminPermissionsIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public AdminPermissionsIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetTokenAsync(string email, string password)
        {
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Token generation failed");
        }

        [Fact]
        public async Task Admin_EditNonDescendantUser_ShouldReturnForbidden()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Get seeded Admin and Superadmin
            var adminUser = await context.Users.FirstAsync(u => u.Email == "admin@test.com");
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

            // Make sure admin's parent is superAdmin (not null) so they are in a hierarchy
            adminUser.ParentUserId = superAdmin.Id;
            context.Users.Update(adminUser);

            // Create a child user under Admin
            var childUser = new User
            {
                Name = "Child User Int",
                Email = $"child_int_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3, // User role
                ParentUserId = adminUser.Id,
                IsActive = true
            };

            // Create a stranger user (parent is SuperAdmin)
            var strangerUser = new User
            {
                Name = "Stranger User Int",
                Email = $"stranger_int_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3, // User role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };

            context.Users.AddRange(childUser, strangerUser);
            await context.SaveChangesAsync();

            // Login as Admin
            var adminToken = await GetTokenAsync("admin@test.com", "admin123");
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Act 1: Update Stranger User (should fail with 403 Forbidden)
            var updateRequestStranger = new UpdateUserRequest
            {
                Name = "Updated Stranger Name Int"
            };
            var resUpdateStranger = await client.PutAsJsonAsync($"/api/users/{strangerUser.Id}", updateRequestStranger);

            // Act 2: Delete Stranger User (should fail with 403 Forbidden)
            var resDeleteStranger = await client.DeleteAsync($"/api/users/{strangerUser.Id}");

            // Act 3: Update Child User (should succeed with 200 OK)
            var updateRequestChild = new UpdateUserRequest
            {
                Name = "Updated Child Name Int"
            };
            var resUpdateChild = await client.PutAsJsonAsync($"/api/users/{childUser.Id}", updateRequestChild);

            // Assert
            resUpdateStranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            resDeleteStranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            resUpdateChild.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Admin_RegisterUserUnderNonDescendantParent_ShouldReturnForbidden()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var adminUser = await context.Users.FirstAsync(u => u.Email == "admin@test.com");
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

            var adminToken = await GetTokenAsync("admin@test.com", "admin123");
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Act 1: Register user with parent = SuperAdmin (not descendant of Admin) -> Should fail with 403
            var registerRequestStrangerParent = new RegisterRequest
            {
                Name = "New Stranger Child Int",
                Email = $"newchild_stranger_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                Role = "user",
                ParentUserId = superAdmin.Id
            };
            var resStrangerParent = await client.PostAsJsonAsync("/api/users/register", registerRequestStrangerParent);

            // Act 2: Register user with parent = Admin himself -> Should succeed with 200 OK
            var registerRequestValidParent = new RegisterRequest
            {
                Name = "New Valid Child Int",
                Email = $"newchild_valid_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                Role = "user",
                ParentUserId = adminUser.Id
            };
            var resValidParent = await client.PostAsJsonAsync("/api/users/register", registerRequestValidParent);

            // Assert
            resStrangerParent.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            resValidParent.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Admin_ManageMatriculasForNonDescendantUser_ShouldReturnForbidden()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var adminUser = await context.Users.FirstAsync(u => u.Email == "admin@test.com");
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

            // Create a stranger user (parent is SuperAdmin)
            var strangerUser = new User
            {
                Name = "Stranger User For Matricula Int",
                Email = $"stranger_mat_int_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3, // User role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };

            context.Users.Add(strangerUser);
            await context.SaveChangesAsync();

            var adminToken = await GetTokenAsync("admin@test.com", "admin123");
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            // Act: Try to create a matricula for the stranger user -> Should fail with 403 Forbidden
            var matriculaRequest = new
            {
                UserId = strangerUser.Id,
                MatriculaNumber = $"MATINT{Guid.NewGuid().ToString()[..8].ToUpper()}",
                IsOwner = true,
                Status = "active",
                StartDate = DateTime.UtcNow.ToString("o")
            };
            var response = await client.PostAsJsonAsync("/api/usermatriculas", matriculaRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
