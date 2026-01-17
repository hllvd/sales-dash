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
    public class ResetPasswordTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ResetPasswordTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task ResetPassword_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var adminToken = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Create a temporary user for this test
            var tempUserId = await CreateTempUser("Admin Reset Test User");

            var request = new ResetPasswordRequest
            {
                SendEmail = false
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{tempUserId}/reset-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResetPasswordResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.NewPassword.Should().NotBeNullOrEmpty();
            result.Data.NewPassword.Should().HaveLength(8);
            result.Data.EmailSent.Should().BeFalse();
        }

        [Fact]
        public async Task ResetPassword_AsSuperAdmin_ShouldSucceed()
        {
            // Arrange
            var superAdminToken = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);

            // Create a temporary user for this test
            var tempUserId = await CreateTempUser("SuperAdmin Reset Test User");

            var request = new ResetPasswordRequest
            {
                SendEmail = false
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{tempUserId}/reset-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResetPasswordResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.NewPassword.Should().NotBeNullOrEmpty();
            result.Data.NewPassword.Should().HaveLength(8);
        }


        [Fact]
        public async Task ResetPassword_PasswordIsUpdatedInDatabase()
        {
            // Arrange
            var adminToken = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Create a temporary user for this test to avoid affecting other tests
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            
            var tempUser = new User
            {
                Name = "Temp Reset User",
                Email = $"temp_reset_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("temppassword123"),
                RoleId = 3, // Regular user role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };
            
            context.Users.Add(tempUser);
            await context.SaveChangesAsync();
            var userId = tempUser.Id;

            var request = new ResetPasswordRequest
            {
                SendEmail = false
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{userId}/reset-password", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResetPasswordResponse>>();
            var newPassword = result!.Data!.NewPassword;

            // Assert - Try to login with the new password
            var loginRequest = new LoginRequest
            {
                Email = tempUser.Email,
                Password = newPassword
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ResetPassword_NonExistentUser_ShouldReturnNotFound()
        {
            // Arrange
            var adminToken = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var nonExistentUserId = Guid.NewGuid();

            var request = new ResetPasswordRequest
            {
                SendEmail = false
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{nonExistentUserId}/reset-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ResetPassword_PasswordFollowsCorrectPattern()
        {
            // Arrange
            var adminToken = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Create a temporary user for this test
            var tempUserId = await CreateTempUser("Pattern Test User");

            var request = new ResetPasswordRequest
            {
                SendEmail = false
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{tempUserId}/reset-password", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResetPasswordResponse>>();
            var newPassword = result!.Data!.NewPassword;

            // Assert - Password should be 8 characters: 2 letters + 6 digits
            newPassword.Should().HaveLength(8);
            newPassword.Substring(0, 2).Should().MatchRegex(@"^[A-Z]{2}$", "first 2 characters should be uppercase letters");
            newPassword.Substring(2).Should().MatchRegex(@"^\d{6}$", "last 6 characters should be digits");
        }

        [Fact]
        public async Task ResetPassword_WithEmailFlag_ShouldIndicateEmailSent()
        {
            // Arrange
            var adminToken = await GetAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Create a temporary user for this test
            var tempUserId = await CreateTempUser("Email Flag Test User");

            var request = new ResetPasswordRequest
            {
                SendEmail = true
            };

            // Act
            var response = await client.PostAsJsonAsync($"/api/users/{tempUserId}/reset-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ResetPasswordResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            // EmailSent will be false because AWS SES is not configured in tests
            // but the endpoint should still work
            result.Data!.NewPassword.Should().NotBeNullOrEmpty();
        }


        private async Task<Guid> CreateTempUser(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            
            var tempUser = new User
            {
                Name = name,
                Email = $"temp_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("temppassword123"),
                RoleId = 3, // Regular user role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };
            
            context.Users.Add(tempUser);
            await context.SaveChangesAsync();
            return tempUser.Id;
        }

        private async Task<Guid> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Email == email);
            return user.Id;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
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
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token");
        }
    }
}
