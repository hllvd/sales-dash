using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
                Password = "password123",
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
    }
}
