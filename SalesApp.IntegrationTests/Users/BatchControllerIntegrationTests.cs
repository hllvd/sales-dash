using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Users Tests")]
    public class BatchControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly UsersTestFactory _factory;

        public BatchControllerIntegrationTests(UsersTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task BatchUpdateParent_WithRegularUser_ShouldReturnForbidden()
        {
            // Arrange
            var token = await GetRegularUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchUpdateParentRequest
            {
                ParentEmail = "superadmin@test.com",
                OverrideExisting = true,
                TeamId = 1
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task BatchUpdateParent_WithMissingParentEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchUpdateParentRequest
            {
                ParentEmail = "",
                OverrideExisting = true,
                TeamId = 1
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("E-mail do superior é obrigatório");
        }

        [Fact]
        public async Task BatchUpdateParent_WithNoFilters_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchUpdateParentRequest
            {
                ParentEmail = "superadmin@test.com",
                OverrideExisting = true,
                TeamId = null,
                Matricula = null
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Pelo menos um filtro (Equipe ou Matrícula) deve ser fornecido");
        }

        [Fact]
        public async Task BatchUpdateParent_WithNonExistentParentEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchUpdateParentRequest
            {
                ParentEmail = "nonexistent_parent_xyz@test.com",
                OverrideExisting = true,
                TeamId = 1
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Superior com o e-mail especificado não foi encontrado");
        }

        [Fact]
        public async Task BatchUpdateParent_ByTeamFilter_ShouldUpdateSuccessfullyAndPropagateLevels()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            User parent;
            User userA;
            User userB;
            User childOfA;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Create a team
                team = new Team { Name = $"Batch Team Test {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                // Create a parent user
                parent = new User
                {
                    Name = "Batch Parent User",
                    Email = $"batch_parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };

                // Create other parent user
                var otherParent = new User
                {
                    Name = "Other Parent User",
                    Email = $"other_parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };

                context.Users.AddRange(parent, otherParent);
                await context.SaveChangesAsync();

                // Create team members
                userA = new User
                {
                    Name = "Team Member A",
                    Email = $"batch_user_a_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };

                userB = new User
                {
                    Name = "Team Member B",
                    Email = $"batch_user_b_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = otherParent.Id, // userB already has a different parent
                    IsActive = true
                };

                context.Users.AddRange(userA, userB);
                await context.SaveChangesAsync();

                // Link to Team
                context.UserTeams.Add(new UserTeam { UserInternalId = userA.InternalId, TeamId = team.Id });
                context.UserTeams.Add(new UserTeam { UserInternalId = userB.InternalId, TeamId = team.Id });

                // Add child of A to verify level propagation
                childOfA = new User
                {
                    Name = "Child of A",
                    Email = $"batch_child_a_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = userA.Level + 1,
                    ParentUserId = userA.Id,
                    IsActive = true
                };
                context.Users.Add(childOfA);

                await context.SaveChangesAsync();
            }

            // Test 1: OverrideExisting = false (Should modify UserA, skip UserB because UserB has parent)
            var request1 = new BatchUpdateParentRequest
            {
                ParentEmail = parent.Email,
                OverrideExisting = false,
                TeamId = team.Id
            };

            var response1 = await client.PostAsJsonAsync("/api/batch/users/parent", request1);
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result1!.Success.Should().BeTrue();
            result1.Data!.Modified.Should().ContainSingle(u => u.Email == userA.Email);
            result1.Data.Skipped.Should().ContainSingle(u => u.Email == userB.Email);

            // Test 2: OverrideExisting = true (Should modify UserB too)
            var request2 = new BatchUpdateParentRequest
            {
                ParentEmail = parent.Email,
                OverrideExisting = true,
                TeamId = team.Id
            };

            var response2 = await client.PostAsJsonAsync("/api/batch/users/parent", request2);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result2!.Success.Should().BeTrue();
            // UserA is already assigned to this parent, so it should be skipped with "already assigned" reason.
            result2.Data!.Skipped.Should().ContainSingle(u => u.Email == userA.Email);
            result2.Data.Modified.Should().ContainSingle(u => u.Email == userB.Email);

            // Verify in DB that levels propagated recursively
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dbUserA = await context.Users.FirstAsync(u => u.Id == userA.Id);
                var dbChildA = await context.Users.FirstAsync(u => u.Id == childOfA.Id);

                // Parent level is 1
                // UserA level should be parent level (1) + 1 = 2
                dbUserA.ParentUserId.Should().Be(parent.Id);
                dbUserA.Level.Should().Be(2);

                // Child of A level should be UserA level (2) + 1 = 3
                dbChildA.Level.Should().Be(3);
            }
        }

        [Fact]
        public async Task BatchUpdateParent_ByMatriculaFilter_ShouldUpdateSuccessfully()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Matricula matricula;
            User parent;
            User user;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Create matricula
                matricula = new Matricula
                {
                    MatriculaNumber = $"MAT-BATCH-{Guid.NewGuid().ToString()[..6]}",
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                context.Matriculas.Add(matricula);
                await context.SaveChangesAsync();

                // Create parent
                parent = new User
                {
                    Name = "Batch Parent",
                    Email = $"batch_parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(parent);
                await context.SaveChangesAsync();

                // Create user
                user = new User
                {
                    Name = "Matricula User",
                    Email = $"batch_mat_user_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Link user to matricula
                context.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = user.InternalId,
                    MatriculaId = matricula.Id,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            var request = new BatchUpdateParentRequest
            {
                ParentEmail = parent.Email,
                OverrideExisting = true,
                Matricula = matricula.MatriculaNumber
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Modified.Should().ContainSingle(u => u.Email == user.Email);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dbUser = await context.Users.FirstAsync(u => u.Id == user.Id);
                dbUser.ParentUserId.Should().Be(parent.Id);
                dbUser.Level.Should().Be(2);
            }
        }

        [Fact]
        public async Task BatchUpdateParent_CircularDependency_ShouldSkipUser()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            User parent;
            User child;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                team = new Team { Name = $"Batch Team Cycle {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                parent = new User
                {
                    Name = "Cycle Parent",
                    Email = $"cycle_parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(parent);
                await context.SaveChangesAsync();

                child = new User
                {
                    Name = "Cycle Child",
                    Email = $"cycle_child_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = parent.Id,
                    IsActive = true
                };
                context.Users.Add(child);
                await context.SaveChangesAsync();

                // Add parent to team to filter
                context.UserTeams.Add(new UserTeam { UserInternalId = parent.InternalId, TeamId = team.Id });
                await context.SaveChangesAsync();
            }

            // Try to make parent a child of child. This should trigger cycle detection and skip parent.
            var request = new BatchUpdateParentRequest
            {
                ParentEmail = child.Email,
                OverrideExisting = true,
                TeamId = team.Id
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/parent", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchUpdateParentResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Skipped.Should().ContainSingle(u => u.Email == parent.Email);
            result.Data.Skipped.First(u => u.Email == parent.Email).Reason.Should().Contain("referência circular");
        }

        [Fact]
        public async Task BatchAssignTeam_WithRegularUser_ShouldReturnForbidden()
        {
            // Arrange
            var token = await GetRegularUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = "superadmin@test.com",
                TeamId = 1,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task BatchAssignTeam_WithNoFilters_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = "",
                Matricula = "",
                TeamId = 1,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Informe o e-mail do superior ou a matrícula");
        }

        [Fact]
        public async Task BatchAssignTeam_WithBothFilters_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = "parent@test.com",
                Matricula = "MAT-123",
                TeamId = 1,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Informe apenas o e-mail do superior ou a matrícula, não ambos");
        }

        [Fact]
        public async Task BatchAssignTeam_ByMatriculaFilter_ShouldAssignSuccessfully()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            Matricula matricula;
            User user;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                team = new Team { Name = $"Assign Team Mat Test {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                matricula = new Matricula
                {
                    MatriculaNumber = $"MAT-ASSIGN-{Guid.NewGuid().ToString()[..6]}",
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                context.Matriculas.Add(matricula);
                await context.SaveChangesAsync();

                user = new User
                {
                    Name = "Matricula Assign User",
                    Email = $"batch_assign_user_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    IsActive = true
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                context.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = user.InternalId,
                    MatriculaId = matricula.Id,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            var request = new BatchAssignTeamRequest
            {
                Matricula = matricula.MatriculaNumber,
                TeamId = team.Id,
                OverrideExisting = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Added.Should().ContainSingle(a => a.Email == user.Email);
            result.Data.Skipped.Should().BeEmpty();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var members = await context.UserTeams
                    .Where(ut => ut.TeamId == team.Id)
                    .Select(ut => ut.UserInternalId)
                    .ToListAsync();

                members.Should().ContainSingle(id => id == user.InternalId);
            }
        }

        [Fact]
        public async Task BatchAssignTeam_WithMissingTeamId_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = "superadmin@test.com",
                TeamId = 0,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Identificador da equipe de destino é obrigatório");
        }

        [Fact]
        public async Task BatchAssignTeam_WithNonExistentParent_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = "nonexistent_parent_xyz@test.com",
                TeamId = 1,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Superior com o e-mail especificado não foi encontrado");
        }

        [Fact]
        public async Task BatchAssignTeam_ShouldAddDirectChildrenToTeam()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            User parent;
            User child1;
            User child2;
            User grandchild; // Should not be added since it's not a direct child

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                team = new Team { Name = $"Assign Team Test {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                parent = new User
                {
                    Name = "Parent User",
                    Email = $"parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(parent);
                await context.SaveChangesAsync();

                child1 = new User
                {
                    Name = "Child User 1",
                    Email = $"child1_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = parent.Id,
                    IsActive = true
                };
                child2 = new User
                {
                    Name = "Child User 2",
                    Email = $"child2_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = parent.Id,
                    IsActive = true
                };
                context.Users.AddRange(child1, child2);
                await context.SaveChangesAsync();

                grandchild = new User
                {
                    Name = "Grandchild User",
                    Email = $"grandchild_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 3,
                    ParentUserId = child1.Id,
                    IsActive = true
                };
                context.Users.Add(grandchild);
                await context.SaveChangesAsync();
            }

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = parent.Email,
                TeamId = team.Id,
                OverrideExisting = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Added.Should().HaveCount(2);
            result.Data.Added.Select(a => a.Email).Should().Contain(new[] { child1.Email, child2.Email });
            result.Data.Skipped.Should().BeEmpty();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var members = await context.UserTeams
                    .Where(ut => ut.TeamId == team.Id)
                    .Select(ut => ut.UserInternalId)
                    .ToListAsync();

                members.Should().Contain(child1.InternalId);
                members.Should().Contain(child2.InternalId);
                members.Should().NotContain(grandchild.InternalId);
            }
        }

        [Fact]
        public async Task BatchAssignTeam_WithExistingMember_SkipWhenOverrideOff()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            User parent;
            User child1;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                team = new Team { Name = $"Assign Team Test {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                parent = new User
                {
                    Name = "Parent User",
                    Email = $"parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(parent);
                await context.SaveChangesAsync();

                child1 = new User
                {
                    Name = "Child User 1",
                    Email = $"child1_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = parent.Id,
                    IsActive = true
                };
                context.Users.Add(child1);
                await context.SaveChangesAsync();

                context.UserTeams.Add(new UserTeam { UserInternalId = child1.InternalId, TeamId = team.Id, StartDate = DateTime.UtcNow.AddDays(-10) });
                await context.SaveChangesAsync();
            }

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = parent.Email,
                TeamId = team.Id,
                OverrideExisting = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Added.Should().BeEmpty();
            result.Data.Skipped.Should().HaveCount(1);
            result.Data.Skipped.First().Email.Should().Be(child1.Email);
            result.Data.Skipped.First().Reason.Should().Contain("membro ativo desta equipe");
        }

        [Fact]
        public async Task BatchAssignTeam_WithExistingMember_UpdateWhenOverrideOn()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Team team;
            User parent;
            User child1;
            var oldStartDate = DateTime.UtcNow.AddDays(-10);
            var newStartDate = DateTime.UtcNow.AddDays(5);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                team = new Team { Name = $"Assign Team Test {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                parent = new User
                {
                    Name = "Parent User",
                    Email = $"parent_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 1,
                    IsActive = true
                };
                context.Users.Add(parent);
                await context.SaveChangesAsync();

                child1 = new User
                {
                    Name = "Child User 1",
                    Email = $"child1_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    Level = 2,
                    ParentUserId = parent.Id,
                    IsActive = true
                };
                context.Users.Add(child1);
                await context.SaveChangesAsync();

                context.UserTeams.Add(new UserTeam { UserInternalId = child1.InternalId, TeamId = team.Id, StartDate = oldStartDate });
                await context.SaveChangesAsync();
            }

            var request = new BatchAssignTeamRequest
            {
                ParentEmail = parent.Email,
                TeamId = team.Id,
                StartDate = newStartDate,
                OverrideExisting = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/team/assign", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<BatchAssignTeamResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.Added.Should().HaveCount(1);
            result.Data.Added.First().Email.Should().Be(child1.Email);
            result.Data.Skipped.Should().BeEmpty();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var membership = await context.UserTeams
                    .FirstOrDefaultAsync(ut => ut.TeamId == team.Id && ut.UserInternalId == child1.InternalId);

                membership.Should().NotBeNull();
                membership!.StartDate.Date.Should().Be(newStartDate.Date);
            }
        }

        [Fact]
        public async Task BatchMergeUsers_WithRegularUser_ShouldReturnForbidden()
        {
            // Arrange
            var token = await GetRegularUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new MergeUsersRequest
            {
                Pairs = new List<MergeUserPair>
                {
                    new MergeUserPair { MainEmail = "main@test.com", DuplicateEmail = "dup@test.com" }
                },
                DryRun = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task BatchMergeUsers_WithDryRun_ShouldReturnPreviewMetricsWithoutModifyingDatabase()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            User mainUser;
            User duplicateUser;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                mainUser = new User
                {
                    Name = "Main User DryRun",
                    Email = $"main_dryrun_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };

                duplicateUser = new User
                {
                    Name = "Duplicate User DryRun",
                    Email = $"dup_dryrun_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };

                context.Users.AddRange(mainUser, duplicateUser);
                await context.SaveChangesAsync();

                // Create contract for duplicateUser
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"CNT-DRY-{Guid.NewGuid().ToString()[..6]}",
                    UserInternalId = duplicateUser.InternalId,
                    TotalAmount = 1000,
                    ContractStatusId = 1,
                    IsActive = true
                });

                await context.SaveChangesAsync();
            }

            var request = new MergeUsersRequest
            {
                Pairs = new List<MergeUserPair>
                {
                    new MergeUserPair { MainEmail = mainUser.Email, DuplicateEmail = duplicateUser.Email }
                },
                DeactivateDuplicate = true,
                DryRun = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MergeUsersResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.IsDryRun.Should().BeTrue();
            result.Data.Pairs.Should().HaveCount(1);
            var pair = result.Data.Pairs.First();
            pair.Error.Should().BeNull();
            pair.ContractsMigrated.Should().Be(1);

            // Verify in DB that duplicate contract was NOT changed and duplicate user is STILL ACTIVE
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dbDupUser = await context.Users.FirstAsync(u => u.Id == duplicateUser.Id);
                var dbContract = await context.Contracts.FirstAsync(c => c.UserInternalId == duplicateUser.InternalId);

                dbDupUser.IsActive.Should().BeTrue();
                dbContract.UserInternalId.Should().Be(duplicateUser.InternalId);
            }
        }

        [Fact]
        public async Task BatchMergeUsers_WithCommitAndDeactivate_ShouldMigrateContractsMatriculasChildrenTeamsAndDeactivateDuplicate()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            User mainUser;
            User duplicateUser;
            User childUser;
            Matricula matricula;
            Team team;
            Contract contract;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                mainUser = new User
                {
                    Name = "Main User Commit",
                    Email = $"main_commit_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };

                duplicateUser = new User
                {
                    Name = "Duplicate User Commit",
                    Email = $"dup_commit_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };

                context.Users.AddRange(mainUser, duplicateUser);
                await context.SaveChangesAsync();

                childUser = new User
                {
                    Name = "Child User Of Duplicate",
                    Email = $"child_dup_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    ParentUserId = duplicateUser.Id,
                    IsActive = true
                };
                context.Users.Add(childUser);

                matricula = new Matricula
                {
                    MatriculaNumber = $"MAT-MERGE-{Guid.NewGuid().ToString()[..6]}",
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                context.Matriculas.Add(matricula);

                team = new Team { Name = $"Team Merge {Guid.NewGuid().ToString()[..6]}" };
                context.Teams.Add(team);

                await context.SaveChangesAsync();

                context.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = duplicateUser.InternalId,
                    MatriculaId = matricula.Id,
                    IsOwner = true,
                    IsActive = true
                });

                context.UserTeams.Add(new UserTeam
                {
                    UserInternalId = duplicateUser.InternalId,
                    TeamId = team.Id,
                    StartDate = DateTime.UtcNow
                });

                contract = new Contract
                {
                    ContractNumber = $"CNT-MERGE-{Guid.NewGuid().ToString()[..6]}",
                    UserInternalId = duplicateUser.InternalId,
                    MatriculaId = matricula.Id,
                    TotalAmount = 5000,
                    ContractStatusId = 1,
                    IsActive = true
                };
                context.Contracts.Add(contract);

                await context.SaveChangesAsync();
            }

            var request = new MergeUsersRequest
            {
                Pairs = new List<MergeUserPair>
                {
                    new MergeUserPair { MainEmail = mainUser.Email, DuplicateEmail = duplicateUser.Email }
                },
                DeactivateDuplicate = true,
                DryRun = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/users/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MergeUsersResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.IsDryRun.Should().BeFalse();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dbMainUser = await context.Users.FirstAsync(u => u.Id == mainUser.Id);
                var dbDupUser = await context.Users.FirstAsync(u => u.Id == duplicateUser.Id);
                var dbChild = await context.Users.FirstAsync(u => u.Id == childUser.Id);
                var dbContract = await context.Contracts.FirstAsync(c => c.Id == contract.Id);
                var dbUM = await context.UserMatriculas.FirstOrDefaultAsync(um => um.MatriculaId == matricula.Id);
                var dbUT = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == team.Id);

                // Duplicate user should be deactivated
                dbDupUser.IsActive.Should().BeFalse();

                // Contract should now belong to mainUser and maintain MatriculaId
                dbContract.UserInternalId.Should().Be(mainUser.InternalId);
                dbContract.MatriculaId.Should().Be(matricula.Id);

                // Child user parent should now be mainUser
                dbChild.ParentUserId.Should().Be(mainUser.Id);

                // UserMatricula should now belong to mainUser
                dbUM.Should().NotBeNull();
                dbUM!.UserInternalId.Should().Be(mainUser.InternalId);

                // UserTeam should now belong to mainUser
                dbUT.Should().NotBeNull();
                dbUT!.UserInternalId.Should().Be(mainUser.InternalId);
            }
        }

        [Fact]
        public async Task BatchMergeMatriculas_WithRegularUser_ShouldReturnForbidden()
        {
            // Arrange
            var regularToken = await GetRegularUserToken();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regularToken);

            var request = new MergeMatriculasRequest
            {
                Pairs = new List<MergeMatriculaPair>
                {
                    new MergeMatriculaPair { MainMatricula = "02123", DuplicateMatricula = "2123" }
                }
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/matriculas/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task BatchMergeMatriculas_WithDryRun_ShouldReturnPreviewMetricsWithoutModifyingDatabase()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            string mainMatNumber = $"MAT_MAIN_{Guid.NewGuid().ToString()[..6]}";
            string dupMatNumber = $"MAT_DUP_{Guid.NewGuid().ToString()[..6]}";

            Matricula mainMat, dupMat;
            User testUser;
            Contract contract;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                mainMat = new Matricula { MatriculaNumber = mainMatNumber, Status = "active" };
                dupMat = new Matricula { MatriculaNumber = dupMatNumber, Status = "active" };
                context.Matriculas.AddRange(mainMat, dupMat);
                await context.SaveChangesAsync();

                testUser = new User
                {
                    Name = "Test User",
                    Email = $"mat_test_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();

                // UserMatricula on dupMat
                context.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = testUser.InternalId,
                    MatriculaId = dupMat.Id,
                    IsOwner = true,
                    IsActive = true
                });

                // Contract on dupMat
                contract = new Contract
                {
                    ContractNumber = $"CNT_{Guid.NewGuid().ToString()[..6]}",
                    UserInternalId = testUser.InternalId,
                    MatriculaId = dupMat.Id,
                    TotalAmount = 100,
                    ContractStatusId = 1
                };
                context.Contracts.Add(contract);

                await context.SaveChangesAsync();
            }

            var request = new MergeMatriculasRequest
            {
                Pairs = new List<MergeMatriculaPair>
                {
                    new MergeMatriculaPair { MainMatricula = mainMatNumber, DuplicateMatricula = dupMatNumber }
                },
                DeleteDuplicate = true,
                DryRun = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/matriculas/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MergeMatriculasResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.IsDryRun.Should().BeTrue();
            result.Data.Pairs.Should().HaveCount(1);

            var pairRes = result.Data.Pairs.First();
            pairRes.Error.Should().BeNull();
            pairRes.UserLinksMigrated.Should().Be(1);
            pairRes.ContractsMigrated.Should().Be(1);

            // Verify in DB that records were NOT modified
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dbDupMat = await context.Matriculas.FirstOrDefaultAsync(m => m.Id == dupMat.Id);
                dbDupMat.Should().NotBeNull();

                var dbContract = await context.Contracts.FirstAsync(c => c.Id == contract.Id);
                dbContract.MatriculaId.Should().Be(dupMat.Id);
            }
        }

        [Fact]
        public async Task BatchMergeMatriculas_WithCommitAndDeleteDuplicate_ShouldMigrateLinksAndDeleteDuplicateRow()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            string mainMatNumber = $"MAT_MAIN_{Guid.NewGuid().ToString()[..6]}";
            string dupMatNumber = $"MAT_DUP_{Guid.NewGuid().ToString()[..6]}";

            Matricula mainMat, dupMat;
            User testUser;
            Contract contract;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                mainMat = new Matricula { MatriculaNumber = mainMatNumber, Status = "active" };
                dupMat = new Matricula { MatriculaNumber = dupMatNumber, Status = "active" };
                context.Matriculas.AddRange(mainMat, dupMat);
                await context.SaveChangesAsync();

                testUser = new User
                {
                    Name = "Test User",
                    Email = $"mat_test_{Guid.NewGuid().ToString()[..6]}@test.com",
                    PasswordHash = "xyz",
                    IsActive = true
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();

                // UserMatricula on dupMat (with IsOwner = true)
                context.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = testUser.InternalId,
                    MatriculaId = dupMat.Id,
                    IsOwner = true,
                    IsActive = true
                });

                // Contract on dupMat
                contract = new Contract
                {
                    ContractNumber = $"CNT_{Guid.NewGuid().ToString()[..6]}",
                    UserInternalId = testUser.InternalId,
                    MatriculaId = dupMat.Id,
                    TotalAmount = 100,
                    ContractStatusId = 1
                };
                context.Contracts.Add(contract);

                await context.SaveChangesAsync();
            }

            var request = new MergeMatriculasRequest
            {
                Pairs = new List<MergeMatriculaPair>
                {
                    new MergeMatriculaPair { MainMatricula = mainMatNumber, DuplicateMatricula = dupMatNumber }
                },
                DeleteDuplicate = true,
                DryRun = false
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/batch/matriculas/merge", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MergeMatriculasResult>>();
            result!.Success.Should().BeTrue();
            result.Data!.IsDryRun.Should().BeFalse();

            var pairRes = result.Data.Pairs.First();
            pairRes.DuplicateDeleted.Should().BeTrue();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Duplicate matricula should be deleted from DB
                var dbDupMat = await context.Matriculas.FirstOrDefaultAsync(m => m.Id == dupMat.Id);
                dbDupMat.Should().BeNull();

                // Contract should now reference mainMat.Id
                var dbContract = await context.Contracts.FirstAsync(c => c.Id == contract.Id);
                dbContract.MatriculaId.Should().Be(mainMat.Id);

                // UserMatricula should now reference mainMat.Id and preserve IsOwner = true
                var dbUM = await context.UserMatriculas.FirstOrDefaultAsync(um => um.UserInternalId == testUser.InternalId);
                dbUM.Should().NotBeNull();
                dbUM!.MatriculaId.Should().Be(mainMat.Id);
                dbUM.IsOwner.Should().BeTrue();
            }
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

        private async Task<string> GetRegularUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get regular user token");
        }
    }
}

