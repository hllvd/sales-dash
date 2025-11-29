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
    public class UsersControllerMatriculaTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UsersControllerMatriculaTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task CreateUser_WithMatricula_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var parentId = (await GetCurrentUser(client)).Id;

            var request = new RegisterRequest
            {
                Name = "Matricula User",
                Email = $"matricula_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = "MAT-001",
                IsMatriculaOwner = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Matricula.Should().Be("MAT-001");
            result.Data!.IsMatriculaOwner.Should().BeFalse();
        }

        [Fact]
        public async Task CreateUser_WithMatriculaAndOwner_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var parentId = (await GetCurrentUser(client)).Id;

            var request = new RegisterRequest
            {
                Name = "Matricula Owner",
                Email = $"owner_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = "MAT-002",
                IsMatriculaOwner = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Matricula.Should().Be("MAT-002");
            result.Data!.IsMatriculaOwner.Should().BeTrue();
        }

        [Fact]
        public async Task CreateUser_SecondOwnerSameMatricula_ShouldFail()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var parentId = (await GetCurrentUser(client)).Id;
            var matricula = "MAT-003";

            // Create first owner
            var owner1 = new RegisterRequest
            {
                Name = "Owner 1",
                Email = $"owner1_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = matricula,
                IsMatriculaOwner = true
            };
            await client.PostAsJsonAsync("/api/users/register", owner1);

            // Create second owner
            var owner2 = new RegisterRequest
            {
                Name = "Owner 2",
                Email = $"owner2_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = matricula,
                IsMatriculaOwner = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/register", owner2);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("already has an owner");
        }

        [Fact]
        public async Task CreateUser_NonOwnerSameMatricula_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var parentId = (await GetCurrentUser(client)).Id;
            var matricula = "MAT-004";

            // Create owner
            var owner = new RegisterRequest
            {
                Name = "Owner",
                Email = $"owner_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = matricula,
                IsMatriculaOwner = true
            };
            await client.PostAsJsonAsync("/api/users/register", owner);

            // Create non-owner
            var nonOwner = new RegisterRequest
            {
                Name = "Non Owner",
                Email = $"nonowner_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "password123",
                ParentUserId = parentId,
                Matricula = matricula,
                IsMatriculaOwner = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/users/register", nonOwner);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Matricula.Should().Be(matricula);
            result.Data!.IsMatriculaOwner.Should().BeFalse();
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
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }
    }
}
