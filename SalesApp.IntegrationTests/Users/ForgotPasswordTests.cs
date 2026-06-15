using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.DTOs;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Integration Tests")]
    public class ForgotPasswordTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ForgotPasswordTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task ForgotPassword_ValidExistingEmail_Returns200WithGenericMessage()
        {
            // Arrange
            var client = _factory.Client;
            var tempUserEmail = await CreateTempUser("Valid Recover User");

            var request = new ForgotPasswordRequest
            {
                Email = tempUserEmail
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/forgot-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().Be("Se este e-mail estiver cadastrado, uma nova senha será enviada em breve.");
        }

        [Fact]
        public async Task ForgotPassword_NonExistentEmail_Returns200WithSameGenericMessage()
        {
            // Arrange
            var client = _factory.Client;
            var request = new ForgotPasswordRequest
            {
                Email = $"nonexistent_{Guid.NewGuid().ToString()[..8]}@test.com"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/forgot-password", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().Be("Se este e-mail estiver cadastrado, uma nova senha será enviada em breve.");
        }

        [Fact]
        public async Task ForgotPassword_InvalidEmail_Returns400()
        {
            // Arrange
            var client = _factory.Client;
            
            // Scenario 1: Empty email
            var requestEmpty = new ForgotPasswordRequest
            {
                Email = ""
            };

            // Act 1
            var responseEmpty = await client.PostAsJsonAsync("/api/users/forgot-password", requestEmpty);

            // Assert 1
            responseEmpty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Scenario 2: Malformed email
            var requestMalformed = new ForgotPasswordRequest
            {
                Email = "notanemail"
            };

            // Act 2
            var responseMalformed = await client.PostAsJsonAsync("/api/users/forgot-password", requestMalformed);

            // Assert 2
            responseMalformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ForgotPassword_CanLoginWithNewPassword()
        {
            // Arrange
            var testEmailSender = new TestEmailSender();
            var client = await _factory.CreateClientWithServicesAsync(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IEmailSender>(testEmailSender);
            });

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            
            var tempUser = new User
            {
                Name = "Temp Recovery User",
                Email = $"temp_recovery_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("temppassword123"),
                RoleId = 3, // Regular user role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };
            
            context.Users.Add(tempUser);
            await context.SaveChangesAsync();

            var forgotPasswordRequest = new ForgotPasswordRequest
            {
                Email = tempUser.Email
            };

            // Act - Request password recovery
            var response = await client.PostAsJsonAsync("/api/users/forgot-password", forgotPasswordRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert - Get the email sent
            testEmailSender.LastMessage.Should().NotBeNull();
            testEmailSender.LastMessage!.To.Should().Be(tempUser.Email);
            
            // Extract the new password from the email body
            var match = Regex.Match(testEmailSender.LastMessage.Body, @"<p class=""password"">([A-Z]{2}\d{6})</p>");
            match.Success.Should().BeTrue();
            var newPassword = match.Groups[1].Value;

            // Attempt login with the new password
            var loginRequest = new LoginRequest
            {
                Email = tempUser.Email,
                Password = newPassword
            };

            var loginResponse = await client.PostAsJsonAsync("/api/users/login", loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // Attempt login with old password - should fail
            var loginRequestOld = new LoginRequest
            {
                Email = tempUser.Email,
                Password = "temppassword123"
            };

            var loginResponseOld = await client.PostAsJsonAsync("/api/users/login", loginRequestOld);
            loginResponseOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        private async Task<string> CreateTempUser(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            
            var email = $"temp_{Guid.NewGuid().ToString()[..8]}@test.com";
            var tempUser = new User
            {
                Name = name,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("temppassword123"),
                RoleId = 3, // Regular user role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };
            
            context.Users.Add(tempUser);
            await context.SaveChangesAsync();
            return email;
        }

        private class TestEmailSender : IEmailSender
        {
            public EmailMessage? LastMessage { get; private set; }

            public Task<bool> SendEmailAsync(EmailMessage message)
            {
                LastMessage = message;
                return Task.FromResult(true);
            }
        }
    }
}
