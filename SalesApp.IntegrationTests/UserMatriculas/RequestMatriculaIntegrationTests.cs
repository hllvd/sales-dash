using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
using Xunit;

namespace SalesApp.IntegrationTests.UserMatriculas
{
    public class RequestMatriculaIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public RequestMatriculaIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetUserTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "user@test.com",
                password = "user123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        private async Task<string> GetAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "admin@test.com",
                password = "admin123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task RequestMatricula_ValidRequest_Returns200AndCreatesMatricula()
        {
            // Arrange
            var token = await GetUserTokenAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new RequestMatriculaRequest
            {
                MatriculaNumber = "TEST-MAT-001"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaInfo>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.MatriculaNumber.Should().Be("TEST-MAT-001");
            result.Data.Status.Should().Be("pending");
            result.Data.IsOwner.Should().BeFalse();
            result.Message.Should().Contain("submitted successfully");
        }

        [Fact]
        public async Task RequestMatricula_DuplicateRequest_Returns400BadRequest()
        {
            // Arrange
            var token = await GetUserTokenAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new RequestMatriculaRequest
            {
                MatriculaNumber = "TEST-MAT-DUP-002"
            };

            // First request
            await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Act - Second request with same matricula
            var response = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaInfo>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("already have this matricula");
        }

        [Fact]
        public async Task RequestMatricula_WithoutAuthentication_Returns401Unauthorized()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = null;
            var request = new RequestMatriculaRequest
            {
                MatriculaNumber = "TEST-MAT-UNAUTH"
            };

            // Act - No authentication header
            var response = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task RequestMatricula_MultipleUsers_CanRequestSameMatricula()
        {
            // Arrange
            var userToken = await GetUserTokenAsync();
            var adminToken = await GetAdminTokenAsync();

            var request = new RequestMatriculaRequest
            {
                MatriculaNumber = "SHARED-MAT-003"
            };

            // Act - User requests matricula
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);
            var response1 = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Admin requests same matricula
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
            var response2 = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaInfo>>();
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaInfo>>();

            result1!.Success.Should().BeTrue();
            result2!.Success.Should().BeTrue();
            result1.Data!.MatriculaNumber.Should().Be("SHARED-MAT-003");
            result2.Data!.MatriculaNumber.Should().Be("SHARED-MAT-003");
        }

        [Fact]
        public async Task RequestMatricula_CreatesWithPendingStatus()
        {
            // Arrange
            var token = await GetUserTokenAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new RequestMatriculaRequest
            {
                MatriculaNumber = "STATUS-TEST-004"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/users/me/request-matricula", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaInfo>>();
            result.Should().NotBeNull();
            result!.Data.Should().NotBeNull();
            result.Data!.Status.Should().Be("pending", "newly requested matriculas should have pending status");
        }
    }
}
