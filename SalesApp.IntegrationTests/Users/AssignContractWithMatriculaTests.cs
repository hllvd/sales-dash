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
    public class AssignContractWithMatriculaTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public AssignContractWithMatriculaTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task AssignContract_WithValidMatricula_ShouldSucceed()
        {
            // Arrange
            var userId = await GetUserIdByEmail("user@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            // Create a matricula for the user
            var matricula = await CreateMatricula(userId, "MAT-TEST-001");
            
            // Create a contract
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}?matriculaNumber={matricula.MatriculaNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.UserId.Should().Be(userId);
            result.Data.MatriculaNumber.Should().Be(matricula.MatriculaNumber);
            
            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbContract = await context.Contracts.FindAsync(contract.Id);
            dbContract.Should().NotBeNull();
            dbContract!.UserId.Should().Be(userId);
            dbContract.MatriculaId.Should().Be(matricula.Id);
        }

        [Fact]
        public async Task AssignContract_WithoutMatricula_ShouldSucceed()
        {
            // Arrange - backward compatibility test
            var userId = await GetUserIdByEmail("user@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.UserId.Should().Be(userId);
            result.Data.MatriculaNumber.Should().BeNull();
        }

        [Fact]
        public async Task AssignContract_WithInactiveMatricula_ShouldReturnBadRequest()
        {
            // Arrange
            var userId = await GetUserIdByEmail("user@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            // Create an inactive matricula
            var matricula = await CreateMatricula(userId, "MAT-INACTIVE-001", isActive: false);
            
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}?matriculaNumber={matricula.MatriculaNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("não está ativa");
        }

        [Fact]
        public async Task AssignContract_WithExpiredMatricula_ShouldReturnBadRequest()
        {
            // Arrange
            var userId = await GetUserIdByEmail("user@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            // Create an expired matricula
            var matricula = await CreateMatricula(userId, "MAT-EXPIRED-001", endDate: DateTime.UtcNow.AddDays(-1));
            
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}?matriculaNumber={matricula.MatriculaNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("expirou");
        }

        [Fact]
        public async Task AssignContract_WithMatriculaBelongingToAnotherUser_ShouldReturnBadRequest()
        {
            // Arrange
            var userId = await GetUserIdByEmail("user@test.com");
            var adminUserId = await GetUserIdByEmail("admin@test.com");
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            
            // Create a matricula for admin
            var matricula = await CreateMatricula(adminUserId, "MAT-ADMIN-001");
            
            var contract = await CreateContract(superAdminUserId);
            
            // Try to assign as regular user with admin's matricula
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}?matriculaNumber={matricula.MatriculaNumber}", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("não pertence a você");
        }

        [Fact]
        public async Task AssignContract_WithNonExistentMatricula_ShouldReturnBadRequest()
        {
            // Arrange
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            var contract = await CreateContract(superAdminUserId);
            
            var token = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}?matriculaNumber=NON-EXISTENT", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("não encontrada");
        }

        // Helper methods
        private async Task<UserMatricula> CreateMatricula(
            Guid userId, 
            string matriculaNumber, 
            bool isActive = true,
            DateTime? endDate = null)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaNumber = matriculaNumber,
                IsActive = isActive,
                IsOwner = true,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = endDate
            };
            
            context.UserMatriculas.Add(matricula);
            await context.SaveChangesAsync();
            
            return matricula;
        }

        private async Task<Contract> CreateContract(Guid userId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Ensure group exists
            var group = await context.Groups.FirstOrDefaultAsync();
            if (group == null)
            {
                group = new Group { Name = "Test Group", Description = "Test Group" };
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }
            
            var contract = new Contract
            {
                ContractNumber = $"CN-{Guid.NewGuid().ToString()[..8]}",
                UserId = userId,
                TotalAmount = 1000,
                GroupId = group.Id,
                Status = "active",
                IsActive = true
            };
            
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();
            
            return contract;
        }

        private async Task<Guid> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Email == email);
            return user.Id;
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"User login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token from login response");
        }
    }
}
