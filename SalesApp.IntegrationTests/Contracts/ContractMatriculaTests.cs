using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Integration Tests")]
    public class ContractMatriculaTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ContractMatriculaTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task CreateContract_WithMatriculaNumber_ShouldAssignCorrectMatriculaId()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a user
            var userId = await CreateTestUserAsync("Matricula Test User");

            // Create a matricula for the user
            var matriculaNumber = $"MAT-{Guid.NewGuid().ToString()[..8]}";
            var matriculaId = await CreateMatriculaAsync(userId, matriculaNumber);

            // Create contract with matricula number
            var contractRequest = new
            {
                contractNumber = $"C-{Guid.NewGuid().ToString()[..8]}",
                userId = userId,
                totalAmount = 1000.00m,
                status = "Active",
                contractStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                matriculaNumber = matriculaNumber
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/contracts", contractRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.MatriculaId.Should().Be(matriculaId);
            result.Data.MatriculaNumber.Should().Be(matriculaNumber);
        }

        [Fact]
        public async Task UpdateContract_WithMatriculaNumber_ShouldUpdateMatriculaId()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = await CreateTestUserAsync("Update Matricula User");
            var contractId = await CreateTestContractAsync(userId);

            // Create two matriculas for the same user
            var matricula1 = $"MAT1-{Guid.NewGuid().ToString()[..8]}";
            var matricula2 = $"MAT2-{Guid.NewGuid().ToString()[..8]}";
            await CreateMatriculaAsync(userId, matricula1);
            var matricula2Id = await CreateMatriculaAsync(userId, matricula2);

            // Update contract with second matricula
            var updateRequest = new
            {
                matriculaNumber = matricula2
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contractId}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Data!.MatriculaId.Should().Be(matricula2Id);
            result.Data.MatriculaNumber.Should().Be(matricula2);
        }

        [Fact]
        public async Task UpdateContract_WithMatriculaFromDifferentUser_ShouldFail()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user1Id = await CreateTestUserAsync("User 1");
            var user2Id = await CreateTestUserAsync("User 2");

            // Create matricula for user 2
            var matriculaNumber = $"MAT-{Guid.NewGuid().ToString()[..8]}";
            await CreateMatriculaAsync(user2Id, matriculaNumber);

            // Create contract for user 1
            var contractId = await CreateTestContractAsync(user1Id);

            // Try to assign user 2's matricula to user 1's contract
            var updateRequest = new
            {
                matriculaNumber = matriculaNumber
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/contracts/{contractId}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("not found for this user");
        }

        [Fact]
        public async Task UpdateContract_WithSameMatriculaNumberForDifferentUsers_ShouldUseCorrectMatricula()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var user1Id = await CreateTestUserAsync("User A");
            var user2Id = await CreateTestUserAsync("User B");

            // Use unique matricula numbers for each user as MatriculaNumber is now unique
            var matricula1 = "1-A";
            var matricula2 = "1-B";
            var user1MatriculaId = await CreateMatriculaAsync(user1Id, matricula1);
            var user2MatriculaId = await CreateMatriculaAsync(user2Id, matricula2);

            // Create contracts for both users
            var contract1Id = await CreateTestContractAsync(user1Id);
            var contract2Id = await CreateTestContractAsync(user2Id);

            // Act
            var response1 = await _client.PutAsJsonAsync($"/api/contracts/{contract1Id}", new { matriculaNumber = matricula1 });
            var response2 = await _client.PutAsJsonAsync($"/api/contracts/{contract2Id}", new { matriculaNumber = matricula2 });
 
            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
 
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
 
            // Each contract should have the correct user's matricula
            result1!.Data!.MatriculaId.Should().Be(user1MatriculaId);
            result1.Data.MatriculaNumber.Should().Be(matricula1);
 
            result2!.Data!.MatriculaId.Should().Be(user2MatriculaId);
            result2.Data.MatriculaNumber.Should().Be(matricula2);
 
            // Verify they're different matricula IDs
            user1MatriculaId.Should().NotBe(user2MatriculaId);
        }

        [Fact]
        public async Task GetUsers_ShouldReturnMatriculaInformation()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create user with matricula
            var userId = await CreateTestUserAsync("User With Matricula");
            var matriculaNumber = $"MAT-{Guid.NewGuid().ToString()[..8]}";
            var matriculaId = await CreateMatriculaAsync(userId, matriculaNumber, isOwner: true);

            // Act
            var response = await _client.GetAsync("/api/users?page=1&pageSize=1000");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            
            var user = result.Data!.Items.FirstOrDefault(u => u.Id == userId);
            user.Should().NotBeNull();
            user!.MatriculaId.Should().Be(matriculaId);
            user.MatriculaNumber.Should().Be(matriculaNumber);
            user.IsMatriculaOwner.Should().BeTrue();
        }

        [Fact]
        public async Task GetUsers_WithoutMatricula_ShouldReturnNullMatriculaFields()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create user without matricula
            var userId = await CreateTestUserAsync("User Without Matricula");

            // Act
            var response = await _client.GetAsync("/api/users?page=1&pageSize=1000");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            
            var user = result!.Data!.Items.FirstOrDefault(u => u.Id == userId);
            user.Should().NotBeNull();
            user!.MatriculaId.Should().BeNull();
            user.MatriculaNumber.Should().BeNull();
            user.IsMatriculaOwner.Should().BeFalse();
        }

        [Fact]
        public async Task GetContracts_ShouldReturnMatriculaNumber()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var userId = await CreateTestUserAsync("Contract User");
            var matriculaNumber = $"MAT-{Guid.NewGuid().ToString()[..8]}";
            var matriculaId = await CreateMatriculaAsync(userId, matriculaNumber);
            
            // Create contract with matricula
            var contractRequest = new
            {
                contractNumber = $"C-{Guid.NewGuid().ToString()[..8]}",
                userId = userId,
                totalAmount = 1000.00m,
                status = "Active",
                contractStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                matriculaNumber = matriculaNumber
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/contracts", contractRequest);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            var contractId = createResult!.Data!.Id;

            // Act
            var response = await _client.GetAsync("/api/contracts");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            
            var contract = result!.Data!.FirstOrDefault(c => c.Id == contractId);
            contract.Should().NotBeNull();
            contract!.MatriculaId.Should().Be(matriculaId);
            contract.MatriculaNumber.Should().Be(matriculaNumber);
        }

        [Fact]
        public async Task GetContracts_AsAdmin_ShouldOnlySeeContractsForAssignedMatriculas_EvenWhenNotOwner()
        {
            // Arrange
            // 1. Create an admin user 
            var adminEmail = $"admin_{Guid.NewGuid().ToString()[..8]}@test.com";
            var adminId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "superadmin@test.com");
                var admin = new User
                {
                    Id = adminId,
                    Name = "Test Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    RoleId = 2, // Admin 
                    ParentUserId = superAdmin?.Id, // Attach to superadmin as parent
                    IsActive = true
                };
                context.Users.Add(admin);
                await context.SaveChangesAsync();
            }

            var matricula1 = $"MAT-ADM1-{Guid.NewGuid().ToString()[..8]}";
            var matricula2 = $"MAT-ADM2-{Guid.NewGuid().ToString()[..8]}";
            
            // Assign as non-owner (IsOwner = false)
            var m1Id = await CreateMatriculaAsync(adminId, matricula1, isOwner: false);
            var m2Id = await CreateMatriculaAsync(adminId, matricula2, isOwner: false);

            // Create another user 
            var otherUserId = await CreateTestUserAsync("Other User");
            var matriculaOther = $"MAT-OTH-{Guid.NewGuid().ToString()[..8]}";
            var mOtherId = await CreateMatriculaAsync(otherUserId, matriculaOther, isOwner: true);

            // Create contracts directly into the DB with these matriculas
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract1 = new Contract { ContractNumber = $"C-{Guid.NewGuid().ToString()[..8]}", UserId = adminId, TempMatricula = matricula1, MatriculaId = m1Id, TotalAmount = 100, Status = "Active", IsActive = true };
                var contract2 = new Contract { ContractNumber = $"C-{Guid.NewGuid().ToString()[..8]}", UserId = null, TempMatricula = matricula2, MatriculaId = m2Id, TotalAmount = 200, Status = "Active", IsActive = true }; // Ensure unassigned contracts are still visible if matricula matches!
                var contractOther = new Contract { ContractNumber = $"C-{Guid.NewGuid().ToString()[..8]}", UserId = otherUserId, TempMatricula = matriculaOther, MatriculaId = mOtherId, TotalAmount = 300, Status = "Active", IsActive = true };
                
                context.Contracts.AddRange(contract1, contract2, contractOther);
                await context.SaveChangesAsync();
            }

            // Act
            var adminToken = await GetUserTokenAsync(adminEmail, "password123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _client.GetAsync("/api/contracts");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            
            var contracts = result.Data!;
            // The admin should see contracts associated with matricula1 and matricula2.
            contracts.Should().Contain(c => c.MatriculaNumber == matricula1);
            contracts.Should().Contain(c => c.MatriculaNumber == matricula2);
            
            // The admin should NOT see contractOther
            contracts.Should().NotContain(c => c.MatriculaNumber == matriculaOther);
        }

        // Helper methods
        private async Task<string> GetUserTokenAsync(string email, string password)
        {
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception($"Failed to get token for {email}");
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get token");
        }

        private async Task<Guid> CreateTestUserAsync(string name)
        {
            var email = $"{name.Replace(" ", "").ToLower()}-{Guid.NewGuid().ToString()[..8]}@test.com";
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            
            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3, // User role
                ParentUserId = superAdmin.Id,
                IsActive = true
            };
            
            context.Users.Add(user);
            await context.SaveChangesAsync();
            
            return user.Id;
        }

        private async Task<int> CreateMatriculaAsync(Guid userId, string matriculaNumber, bool isOwner = true)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var m = new Matricula
            {
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                Status = "active"
            };
            context.Matriculas.Add(m);
            await context.SaveChangesAsync();

            var userMatricula = new UserMatricula
            {
                UserId = userId,
                MatriculaId = m.Id,
                IsActive = true,
                IsOwner = isOwner
            };
            
            context.UserMatriculas.Add(userMatricula);
            await context.SaveChangesAsync();
            
            return m.Id;
        }

        private async Task<int> CreateTestContractAsync(Guid userId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var contract = new Contract
            {
                ContractNumber = $"C-{Guid.NewGuid().ToString()[..8]}",
                UserId = userId,
                TotalAmount = 1000.00m,
                Status = "Active",
                SaleStartDate = DateTime.UtcNow,
                IsActive = true
            };
            
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();
            
            return contract.Id;
        }
    }
}
