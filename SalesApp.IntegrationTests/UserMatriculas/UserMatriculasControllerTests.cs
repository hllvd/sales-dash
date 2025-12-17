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
    public class UserMatriculasControllerTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public UserMatriculasControllerTests(TestWebApplicationFactory factory)
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
        public async Task CreateUserMatricula_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "Matricula Test User", Email = "mattest@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var request = new CreateUserMatriculaRequest
            {
                UserId = userId,
                MatriculaNumber = "MAT-INT-001",
                StartDate = DateTime.UtcNow
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usermatriculas", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.MatriculaNumber.Should().Be("MAT-INT-001");
            result.Data.UserId.Should().Be(userId);
        }

        [Fact]
        public async Task GetAllUserMatriculas_AsAdmin_ShouldReturnList()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/usermatriculas");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserMatriculaResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUserMatriculaById_AsAdmin_ShouldReturnMatricula()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = Guid.NewGuid();
            int matriculaId;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "Get Test User", Email = "gettest@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var matricula = new UserMatricula
                {
                    UserId = userId,
                    MatriculaNumber = "MAT-INT-002",
                    StartDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.UserMatriculas.Add(matricula);
                await context.SaveChangesAsync();
                matriculaId = matricula.Id;
            }

            // Act
            var response = await _client.GetAsync($"/api/usermatriculas/{matriculaId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Id.Should().Be(matriculaId);
            result.Data.MatriculaNumber.Should().Be("MAT-INT-002");
        }

        [Fact]
        public async Task GetUserMatriculasByUserId_AsAdmin_ShouldReturnUserMatriculas()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = Guid.NewGuid();
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "Multi Mat User", Email = "multimat@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var matricula1 = new UserMatricula { UserId = userId, MatriculaNumber = "MAT-INT-003", StartDate = DateTime.UtcNow, IsActive = true };
                var matricula2 = new UserMatricula { UserId = userId, MatriculaNumber = "MAT-INT-004", StartDate = DateTime.UtcNow, IsActive = true };
                context.UserMatriculas.AddRange(matricula1, matricula2);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/usermatriculas/user/{userId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserMatriculaResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);
            result.Data.Should().Contain(m => m.MatriculaNumber == "MAT-INT-003");
            result.Data.Should().Contain(m => m.MatriculaNumber == "MAT-INT-004");
        }

        [Fact]
        public async Task UpdateUserMatricula_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = Guid.NewGuid();
            int matriculaId;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "Update Test User", Email = "updatetest@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var matricula = new UserMatricula
                {
                    UserId = userId,
                    MatriculaNumber = "MAT-INT-005",
                    StartDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.UserMatriculas.Add(matricula);
                await context.SaveChangesAsync();
                matriculaId = matricula.Id;
            }

            var updateRequest = new UpdateUserMatriculaRequest
            {
                IsActive = false,
                EndDate = DateTime.UtcNow
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/usermatriculas/{matriculaId}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.IsActive.Should().BeFalse();
            result.Data.EndDate.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteUserMatricula_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = Guid.NewGuid();
            int matriculaId;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Id = userId, Name = "Delete Test User", Email = "deletetest@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var matricula = new UserMatricula
                {
                    UserId = userId,
                    MatriculaNumber = "MAT-INT-006",
                    StartDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.UserMatriculas.Add(matricula);
                await context.SaveChangesAsync();
                matriculaId = matricula.Id;
            }

            // Act
            var response = await _client.DeleteAsync($"/api/usermatriculas/{matriculaId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // Verify it's deleted
            var getResponse = await _client.GetAsync($"/api/usermatriculas/{matriculaId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task BulkAssignMatriculas_AsAdmin_ShouldCreateMultiple()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user1 = new User { Id = user1Id, Name = "Bulk User 1", Email = "bulk1@test.com", PasswordHash = "hash", RoleId = 1 };
                var user2 = new User { Id = user2Id, Name = "Bulk User 2", Email = "bulk2@test.com", PasswordHash = "hash", RoleId = 1 };
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();
            }

            var bulkRequest = new BulkAssignMatriculasRequest
            {
                Assignments = new List<MatriculaAssignment>
                {
                    new MatriculaAssignment { UserId = user1Id, MatriculaNumber = "BULK-001", StartDate = DateTime.UtcNow },
                    new MatriculaAssignment { UserId = user2Id, MatriculaNumber = "BULK-002", StartDate = DateTime.UtcNow }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usermatriculas/bulk-assign", bulkRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkAssignResult>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.SuccessCount.Should().Be(2);
            result.Data.ErrorCount.Should().Be(0);
            result.Data.Created.Should().HaveCount(2);
        }

        [Fact]
        public async Task CreateUserMatricula_WithNonExistentUser_ShouldFail()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new CreateUserMatriculaRequest
            {
                UserId = Guid.NewGuid(), // Non-existent user
                MatriculaNumber = "MAT-FAIL-001",
                StartDate = DateTime.UtcNow
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usermatriculas", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
