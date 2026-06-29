using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Integration Tests")]
    public class ContractMigrationTests
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ContractMigrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetTokenAsync(string email, string password)
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new LoginRequest
            {
                Email = email,
                Password = password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        private async Task<string> GetSuperAdminTokenAsync() => await GetTokenAsync("superadmin@test.com", "superadmin123");
        private async Task<string> GetAdminTokenAsync() => await GetTokenAsync("admin@test.com", "admin123");
        private async Task<string> GetUserTokenAsync() => await GetTokenAsync("user@test.com", "user123");

        private async Task<User> CreateUserAsync(string name, string email, int roleId, Guid? parentId = null)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = roleId,
                ParentUserId = parentId,
                IsActive = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return await context.Users.FirstAsync(u => u.Id == user.Id);
        }

        private async Task<UserMatricula> CreateUserMatriculaAsync(Guid userId, string matriculaNumber, bool isOwner = true, bool isActive = true)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var matricula = await context.Matriculas.FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber);
            if (matricula == null)
            {
                matricula = new Matricula
                {
                    MatriculaNumber = matriculaNumber,
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    Status = "active"
                };
                context.Matriculas.Add(matricula);
                await context.SaveChangesAsync();
            }

            var user = await context.Users.FirstAsync(u => u.Id == userId);
            var userMatricula = new UserMatricula
            {
                UserInternalId = user.InternalId,
                MatriculaId = matricula.Id,
                IsActive = isActive,
                IsOwner = isOwner
            };

            context.UserMatriculas.Add(userMatricula);
            await context.SaveChangesAsync();

            return await context.UserMatriculas
                .Include(um => um.Matricula)
                .Include(um => um.User)
                .FirstAsync(um => um.Id == userMatricula.Id);
        }

        private async Task<Contract> CreateContractAsync(Guid userId, string contractNumber, string matriculaNumber = null, bool isActive = true)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = await context.Groups.FirstOrDefaultAsync();
            if (group == null)
            {
                group = new Group { Name = "Test Group", Description = "Test Group" };
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            var user = await context.Users.FirstAsync(u => u.Id == userId);
            int? matriculaId = null;

            if (matriculaNumber != null)
            {
                var m = await context.Matriculas.FirstOrDefaultAsync(mat => mat.MatriculaNumber == matriculaNumber);
                if (m == null)
                {
                    m = new Matricula { MatriculaNumber = matriculaNumber, Status = "active" };
                    context.Matriculas.Add(m);
                    await context.SaveChangesAsync();
                }
                matriculaId = m.Id;
            }

            var contract = new Contract
            {
                ContractNumber = contractNumber,
                UserInternalId = user.InternalId,
                TotalAmount = 1500,
                GroupId = group.Id,
                ContractStatusId = 1,
                MatriculaId = matriculaId,
                TempMatricula = matriculaNumber,
                IsActive = isActive
            };

            context.Contracts.Add(contract);
            await context.SaveChangesAsync();

            return await context.Contracts
                .Include(c => c.User)
                .Include(c => c.Matricula)
                .FirstAsync(c => c.Id == contract.Id);
        }

        [Fact]
        public async Task GetMigrationPreview_ShouldReturnContractsAndProposedMatricula_ForSingleMatricula()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create parent with 1 owned matricula, and child user
            var parent = await CreateUserAsync("Parent User 1", "parent1@test.com", 2);
            var parentMat = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-01");

            var child = await CreateUserAsync("Child User 1", "child1@test.com", 3, parent.Id);
            var childMat = await CreateUserMatriculaAsync(child.Id, "MAT-CHILD-01");

            // Create some contracts for child (both active and inactive)
            var c1 = await CreateContractAsync(child.Id, $"CON-MIG-01-{Guid.NewGuid().ToString()[..4]}", childMat.Matricula.MatriculaNumber, isActive: true);
            var c2 = await CreateContractAsync(child.Id, $"CON-MIG-02-{Guid.NewGuid().ToString()[..4]}", childMat.Matricula.MatriculaNumber, isActive: false);

            // Act
            var response = await _client.GetAsync($"/api/contracts/user/{child.Id}/migrate-preview");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractMigrationPreviewItem>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);

            // Both contracts should be mapped to parent's single matricula as target, and set to auto-selected
            foreach (var item in result.Data)
            {
                item.TargetMatriculaId.Should().Be(parentMat.MatriculaId);
                item.TargetMatriculaNumber.Should().Be("MAT-PARENT-01");
                item.IsAutoSelected.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GetMigrationPreview_ShouldShowAllOptions_WhenParentHasMultipleMatriculas()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create parent with 2 owned matriculas, and child user
            var parent = await CreateUserAsync("Parent User 2", "parent2@test.com", 2);
            var parentMat1 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-02A");
            var parentMat2 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-02B");

            var child = await CreateUserAsync("Child User 2", "child2@test.com", 3, parent.Id);

            var c1 = await CreateContractAsync(child.Id, $"CON-MIG-03-{Guid.NewGuid().ToString()[..4]}", "MAT-PARENT-02A");
            var c2 = await CreateContractAsync(child.Id, $"CON-MIG-04-{Guid.NewGuid().ToString()[..4]}", "MAT-OTHER-NUM"); // No match

            // Act
            var response = await _client.GetAsync($"/api/contracts/user/{child.Id}/migrate-preview");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractMigrationPreviewItem>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();

            // C1 is matched to MAT-PARENT-02A (auto-selected) and has MAT-PARENT-02B as option (not auto-selected) -> 2 preview items
            // C2 has no match, so it has MAT-PARENT-02A and MAT-PARENT-02B as options, both not auto-selected -> 2 preview items
            result.Data.Should().HaveCount(4);

            var c1Items = result.Data.Where(i => i.ContractId == c1.Id).ToList();
            c1Items.Should().HaveCount(2);
            c1Items.First(i => i.TargetMatriculaId == parentMat1.MatriculaId).IsAutoSelected.Should().BeTrue();
            c1Items.First(i => i.TargetMatriculaId == parentMat2.MatriculaId).IsAutoSelected.Should().BeFalse();

            var c2Items = result.Data.Where(i => i.ContractId == c2.Id).ToList();
            c2Items.Should().HaveCount(2);
            c2Items.All(i => !i.IsAutoSelected).Should().BeTrue();
        }

        [Fact]
        public async Task MigrateContracts_ShouldSucceed_ForSingleMatricula()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parent = await CreateUserAsync("Parent User 3", "parent3@test.com", 2);
            var parentMat = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-03");

            var child = await CreateUserAsync("Child User 3", "child3@test.com", 3, parent.Id);
            var childMat = await CreateUserMatriculaAsync(child.Id, "MAT-CHILD-03");

            var c1 = await CreateContractAsync(child.Id, $"CON-MIG-05-{Guid.NewGuid().ToString()[..4]}", childMat.Matricula.MatriculaNumber, isActive: true);
            var c2 = await CreateContractAsync(child.Id, $"CON-MIG-06-{Guid.NewGuid().ToString()[..4]}", childMat.Matricula.MatriculaNumber, isActive: false);

            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractMigrationResult>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.MigratedCount.Should().Be(2);

            // Check db updates
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updatedC1 = await context.Contracts.FirstAsync(c => c.Id == c1.Id);
            updatedC1.UserInternalId.Should().Be(parent.InternalId);
            updatedC1.MatriculaId.Should().Be(parentMat.MatriculaId);
            updatedC1.IsActive.Should().BeTrue();

            var updatedC2 = await context.Contracts.FirstAsync(c => c.Id == c2.Id);
            updatedC2.UserInternalId.Should().Be(parent.InternalId);
            updatedC2.MatriculaId.Should().Be(parentMat.MatriculaId);
            updatedC2.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task MigrateContracts_ShouldSucceed_WithMultipleMatriculasAndRequestMapping()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parent = await CreateUserAsync("Parent User 4", "parent4@test.com", 2);
            var parentMat1 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-04A");
            var parentMat2 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-04B");

            var child = await CreateUserAsync("Child User 4", "child4@test.com", 3, parent.Id);
            var c1 = await CreateContractAsync(child.Id, $"CON-MIG-07-{Guid.NewGuid().ToString()[..4]}");

            // We explicitly assign C1 to parentMat2
            var request = new ContractMigrationRequest
            {
                Mappings = new List<ContractMatriculaMapping>
                {
                    new ContractMatriculaMapping { ContractId = c1.Id, TargetMatriculaId = parentMat2.MatriculaId }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractMigrationResult>>();
            result!.Success.Should().BeTrue();

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedC1 = await context.Contracts.FirstAsync(c => c.Id == c1.Id);
            updatedC1.UserInternalId.Should().Be(parent.InternalId);
            updatedC1.MatriculaId.Should().Be(parentMat2.MatriculaId);
        }

        [Fact]
        public async Task MigrateContracts_ShouldFail_WithAmbiguousSelection()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parent = await CreateUserAsync("Parent User 5", "parent5@test.com", 2);
            var parentMat1 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-05A");
            var parentMat2 = await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-05B");

            var child = await CreateUserAsync("Child User 5", "child5@test.com", 3, parent.Id);
            var c1 = await CreateContractAsync(child.Id, $"CON-MIG-08-{Guid.NewGuid().ToString()[..4]}", "MAT-NO-MATCH");

            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractMigrationResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Ambiguous matricula selection");
        }

        [Fact]
        public async Task MigrateContracts_ShouldFail_ForUserWithoutParent()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var child = await CreateUserAsync("Child User 6", "child6@test.com", 3, parentId: null);
            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractMigrationResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("does not have a parent user");
        }

        [Fact]
        public async Task MigrateContracts_ShouldFail_WhenParentHasNoActiveOwnedMatricula()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parent = await CreateUserAsync("Parent User 7", "parent7@test.com", 2);
            // Non-active user matricula mapping
            await CreateUserMatriculaAsync(parent.Id, "MAT-PARENT-07", isOwner: true, isActive: false);

            var child = await CreateUserAsync("Child User 7", "child7@test.com", 3, parent.Id);
            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractMigrationResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("não possui nenhuma matrícula ativa sob sua titularidade");
        }

        [Fact]
        public async Task MigrateContracts_ShouldBeAllowedByAdmin_OnlyForDirectChild()
        {
            // Arrange
            // Create a brand new unique admin user
            var customAdmin = await CreateUserAsync("Custom Admin", $"customadmin-{Guid.NewGuid().ToString()[..4]}@test.com", 2);
            var adminToken = await GetTokenAsync(customAdmin.Email, "password123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Set up a child user whose parent is this admin
            var directChild = await CreateUserAsync("Direct Child", "directchild@test.com", 3, customAdmin.Id);
            await CreateUserMatriculaAsync(customAdmin.Id, "MAT-ADMIN-OWNED");

            // Setup direct child contract
            var c1 = await CreateContractAsync(directChild.Id, $"CON-DIRECT-{Guid.NewGuid().ToString()[..4]}");

            // Set up a child user whose parent is NOT this admin
            var otherParent = await CreateUserAsync("Other Parent", "otherparent@test.com", 2);
            var nonChild = await CreateUserAsync("Non Child", "nonchild@test.com", 3, otherParent.Id);
            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act 1: Migrate direct child -> Should succeed
            var response1 = await _client.PostAsJsonAsync($"/api/contracts/user/{directChild.Id}/migrate", request);
            response1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act 2: Migrate non-child -> Should be Forbidden (403)
            var response2 = await _client.PostAsJsonAsync($"/api/contracts/user/{nonChild.Id}/migrate", request);
            response2.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task MigrateContracts_ShouldBeForbidden_ForRegularUser()
        {
            // Arrange
            var userToken = await GetUserTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            var parent = await CreateUserAsync("Parent User 8", "parent8@test.com", 2);
            var child = await CreateUserAsync("Child User 8", "child8@test.com", 3, parent.Id);
            var request = new ContractMigrationRequest { Mappings = new List<ContractMatriculaMapping>() };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/contracts/user/{child.Id}/migrate", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
