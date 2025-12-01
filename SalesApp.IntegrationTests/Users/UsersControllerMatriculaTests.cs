using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
using SalesApp.IntegrationTests;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    public class UsersControllerMatriculaTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public UsersControllerMatriculaTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetToken(string role)
        {
            // Create a user with the specified role if it doesn't exist (or use existing one)
            // For simplicity in tests, we can use the factory's seeded users or create new ones.
            // The factory seeds: admin@test.com (admin), user@test.com (user), superadmin@test.com (superadmin)
            
            var email = role switch
            {
                "admin" => "admin@test.com",
                "superadmin" => "superadmin@test.com",
                _ => "user@test.com"
            };

            var password = role switch
            {
                "admin" => "admin123",
                "superadmin" => "superadmin123",
                _ => "user123"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = email,
                password = password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data!.Token;
        }

        [Fact]
        public async Task GetByMatricula_AsAdmin_ShouldReturnUsers()
        {
            // Arrange
            var matricula = "MAT_ADMIN_TEST";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User
                {
                    Name = "Matricula Test User",
                    Email = "matricula.admin@test.com",
                    Matricula = matricula,
                    IsActive = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = context.Roles.First(r => r.Name == "user").Id
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/users/by-matricula/{matricula}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserLookupResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().HaveCount(1);
            result.Data![0].Matricula.Should().Be(matricula);
            result.Data![0].Name.Should().Be("Matricula Test User");
        }

        [Fact]
        public async Task GetByMatricula_AsSuperAdmin_ShouldReturnUsers()
        {
            // Arrange
            var matricula = "MAT_SUPERADMIN_TEST";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User
                {
                    Name = "SuperAdmin Test User",
                    Email = "matricula.super@test.com",
                    Matricula = matricula,
                    IsActive = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = context.Roles.First(r => r.Name == "user").Id
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/users/by-matricula/{matricula}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserLookupResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().Contain(u => u.Matricula == matricula);
        }

        [Fact]
        public async Task GetByMatricula_AsRegularUser_ShouldReturn403()
        {
            // Arrange
            var token = await GetToken("user");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/users/by-matricula/ANY_MATRICULA");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetByMatricula_Unauthorized_ShouldReturn401()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync("/api/users/by-matricula/ANY_MATRICULA");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetByMatricula_NotFound_ShouldReturnEmptyList()
        {
            // Arrange
            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/users/by-matricula/NON_EXISTENT_MATRICULA");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserLookupResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByMatricula_MultipleMatches_ShouldReturnAll()
        {
            // Arrange
            var matricula = "MAT_DUPLICATE_TEST";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var roleId = context.Roles.First(r => r.Name == "user").Id;
                
                var user1 = new User
                {
                    Name = "Duplicate User 1",
                    Email = "dup1@test.com",
                    Matricula = matricula,
                    IsActive = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = roleId
                };
                var user2 = new User
                {
                    Name = "Duplicate User 2",
                    Email = "dup2@test.com",
                    Matricula = matricula,
                    IsActive = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = roleId
                };
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();
            }

            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/api/users/by-matricula/{matricula}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserLookupResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);
            result.Data.Should().Contain(u => u.Name == "Duplicate User 1");
            result.Data.Should().Contain(u => u.Name == "Duplicate User 2");
        }
    }
}
