using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.UserMatriculas
{
    public class UserMatriculasIsOwnerIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UserMatriculasIsOwnerIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetAdminToken()
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
        public async Task CreateMatricula_WithIsOwnerTrue_ShouldCreateAsOwner()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var userId = await GetFirstUserId();
            var request = new CreateUserMatriculaRequest
            {
                UserId = userId,
                MatriculaNumber = $"MAT-OWNER-{Guid.NewGuid()}",
                StartDate = DateTime.UtcNow,
                IsOwner = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usermatriculas", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.IsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task CreateMatricula_SecondUserWithSameNumberAndIsOwnerTrue_ShouldTransferOwnership()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var user1Id = await GetFirstUserId();
            var user2Id = await GetSecondUserId();
            var matriculaNumber = $"MAT-TRANSFER-{Guid.NewGuid()}";

            // Create first matricula with IsOwner = true
            var request1 = new CreateUserMatriculaRequest
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true
            };
            var response1 = await _client.PostAsJsonAsync("/api/usermatriculas", request1);
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            var matricula1Id = result1!.Data!.Id;

            // Act - Create second matricula with same number and IsOwner = true
            var request2 = new CreateUserMatriculaRequest
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true
            };
            var response2 = await _client.PostAsJsonAsync("/api/usermatriculas", request2);

            // Assert
            response2.StatusCode.Should().Be(HttpStatusCode.Created);
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            result2!.Data!.IsOwner.Should().BeTrue("User 2 should be owner");

            // Verify first user is no longer owner
            var getResponse1 = await _client.GetAsync($"/api/usermatriculas/{matricula1Id}");
            var getResult1 = await getResponse1.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            getResult1!.Data!.IsOwner.Should().BeFalse("User 1 should no longer be owner");
        }

        [Fact]
        public async Task UpdateMatricula_SetIsOwnerToTrue_ShouldTransferOwnership()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var user1Id = await GetFirstUserId();
            var user2Id = await GetSecondUserId();
            var matriculaNumber = $"MAT-UPDATE-{Guid.NewGuid()}";

            // Create two matriculas, first one is owner
            var request1 = new CreateUserMatriculaRequest
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true
            };
            var response1 = await _client.PostAsJsonAsync("/api/usermatriculas", request1);
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            var matricula1Id = result1!.Data!.Id;

            var request2 = new CreateUserMatriculaRequest
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false
            };
            var response2 = await _client.PostAsJsonAsync("/api/usermatriculas", request2);
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            var matricula2Id = result2!.Data!.Id;

            // Act - Update second matricula to be owner
            var updateRequest = new UpdateUserMatriculaRequest
            {
                IsOwner = true
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/usermatriculas/{matricula2Id}", updateRequest);

            // Assert
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify ownership transfer
            var getResponse1 = await _client.GetAsync($"/api/usermatriculas/{matricula1Id}");
            var getResult1 = await getResponse1.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            getResult1!.Data!.IsOwner.Should().BeFalse("User 1 should no longer be owner");

            var getResponse2 = await _client.GetAsync($"/api/usermatriculas/{matricula2Id}");
            var getResult2 = await getResponse2.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            getResult2!.Data!.IsOwner.Should().BeTrue("User 2 should now be owner");
        }

        [Fact]
        public async Task GetByUserId_ShouldIncludeIsOwnerField()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var userId = await GetFirstUserId();
            var matriculaNumber = $"MAT-GET-{Guid.NewGuid()}";

            var request = new CreateUserMatriculaRequest
            {
                UserId = userId,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true
            };
            await _client.PostAsJsonAsync("/api/usermatriculas", request);

            // Act
            var response = await _client.GetAsync($"/api/usermatriculas/user/{userId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserMatriculaResponse>>>();
            result!.Data.Should().NotBeEmpty();
            result.Data!.Any(m => m.MatriculaNumber == matriculaNumber && m.IsOwner).Should().BeTrue();
        }

        [Fact]
        public async Task MultipleUsers_SameMatricula_OnlyOneOwner()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var user1Id = await GetFirstUserId();
            var user2Id = await GetSecondUserId();
            var matriculaNumber = $"MAT-MULTI-{Guid.NewGuid()}";

            // Create matriculas for both users
            await _client.PostAsJsonAsync("/api/usermatriculas", new CreateUserMatriculaRequest
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true
            });

            await _client.PostAsJsonAsync("/api/usermatriculas", new CreateUserMatriculaRequest
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false
            });

            // Act - Get all matriculas
            var response1 = await _client.GetAsync($"/api/usermatriculas/user/{user1Id}");
            var response2 = await _client.GetAsync($"/api/usermatriculas/user/{user2Id}");

            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<List<UserMatriculaResponse>>>();
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<List<UserMatriculaResponse>>>();

            var user1Matricula = result1!.Data!.First(m => m.MatriculaNumber == matriculaNumber);
            var user2Matricula = result2!.Data!.First(m => m.MatriculaNumber == matriculaNumber);

            // Assert
            var ownerCount = (user1Matricula.IsOwner ? 1 : 0) + (user2Matricula.IsOwner ? 1 : 0);
            ownerCount.Should().Be(1, "Only one user should be owner");
            user1Matricula.IsOwner.Should().BeTrue("User 1 should be owner");
            user2Matricula.IsOwner.Should().BeFalse("User 2 should not be owner");
        }


        private async Task<Guid> GetFirstUserId()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync();
            return user?.Id ?? Guid.Empty;
        }

        private async Task<Guid> GetSecondUserId()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            var user = await context.Users.Skip(1).FirstOrDefaultAsync();
            return user?.Id ?? Guid.Empty;
        }
    }
}
