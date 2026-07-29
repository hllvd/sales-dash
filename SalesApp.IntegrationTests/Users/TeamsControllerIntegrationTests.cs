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
using SalesApp.Models;
using SalesApp.DTOs;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Integration Tests")]
    public class TeamsControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public TeamsControllerIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task CreateTeam_WithValidData_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var teamName = $"Integration Team {Guid.NewGuid()}";
            var request = new CreateTeamRequest
            {
                Name = teamName,
                Members = new List<TeamMemberRequest>()
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Name.Should().Be(teamName);

            // Clean up / Verification in DB
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == teamName);
            dbTeam.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateTeam_WithDuplicateName_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var duplicateName = $"Duplicate Team {Guid.NewGuid()}";
            
            // Seed a team
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Teams.Add(new Team { Name = duplicateName });
                await context.SaveChangesAsync();
            }

            var request = new CreateTeamRequest
            {
                Name = duplicateName,
                Members = new List<TeamMemberRequest>()
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddMembers_WithDefaultDate_ShouldDefaultToEightYearsAgo()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var teamName = $"Default Date Team {Guid.NewGuid()}";
            var userEmail = $"member_{Guid.NewGuid().ToString()[..8]}@test.com";
            Guid userGuid;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                
                var user = new User
                {
                    Name = "Test Member",
                    Email = userEmail,
                    PasswordHash = "fakehash",
                    RoleId = 3,
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
                userGuid = user.Id;
            }

            var createRequest = new CreateTeamRequest
            {
                Name = teamName,
                Members = new List<TeamMemberRequest>
                {
                    new TeamMemberRequest
                    {
                        UserId = userGuid,
                        StartDate = null // trigger default date logic
                    }
                }
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams", createRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>();
            result!.Success.Should().BeTrue();

            var member = result.Data!.Members.FirstOrDefault(m => m.UserId == userGuid);
            member.Should().NotBeNull();
            
            // Should be approximately 8 years ago
            var expectedDefault = DateTime.UtcNow.AddYears(-8);
            member!.StartDate.Should().BeBefore(expectedDefault.AddDays(1));
            member.StartDate.Should().BeAfter(expectedDefault.AddDays(-1));
        }

        [Fact]
        public async Task AddMember_WithOverlapOnOtherTeam_ShouldAutoCloseOverlappingAssignment()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var teamAlphaName = $"Team Alpha {Guid.NewGuid()}";
            var teamBetaName = $"Team Beta {Guid.NewGuid()}";
            var memberEmail = $"overlap_member_{Guid.NewGuid().ToString()[..8]}@test.com";
            
            Guid userGuid;
            int teamAlphaId;
            int teamBetaId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                var user = new User
                {
                    Name = "Overlap Member",
                    Email = memberEmail,
                    PasswordHash = "fakehash",
                    RoleId = 3,
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(user);

                var teamAlpha = new Team { Name = teamAlphaName };
                var teamBeta = new Team { Name = teamBetaName };
                context.Teams.AddRange(teamAlpha, teamBeta);
                await context.SaveChangesAsync();

                userGuid = user.Id;
                teamAlphaId = teamAlpha.Id;
                teamBetaId = teamBeta.Id;

                // Add active membership on Team Alpha (8 years ago to now/null)
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamAlphaId,
                    UserInternalId = user.InternalId,
                    StartDate = DateTime.UtcNow.AddYears(-2),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            // Act - Add member to Team Beta starting today
            var addRequest = new AddMembersRequest
            {
                Members = new List<TeamMemberRequest>
                {
                    new TeamMemberRequest
                    {
                        UserId = userGuid,
                        StartDate = DateTime.UtcNow
                    }
                }
            };

            var response = await client.PostAsJsonAsync($"/api/teams/{teamBetaId}/members", addRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Warnings.Should().NotBeEmpty(); // Overlap warning should be generated
            
            // Verify in DB that Alpha is now closed
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await context.Users.FirstAsync(u => u.Id == userGuid);
                
                var oldMembership = await context.UserTeams
                    .FirstOrDefaultAsync(ut => ut.TeamId == teamAlphaId && ut.UserInternalId == user.InternalId);
                
                oldMembership.Should().NotBeNull();
                oldMembership!.EndDate.Should().NotBeNull(); // Should be closed
                oldMembership.EndDate.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task SetOwner_WithNonMember_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            int teamId;
            Guid nonMemberUserId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                var team = new Team { Name = $"Owner Test Team {Guid.NewGuid()}" };
                context.Teams.Add(team);

                var nonMember = new User
                {
                    Name = "Non Member",
                    Email = $"nonmember_{Guid.NewGuid().ToString()[..8]}@test.com",
                    PasswordHash = "fakehash",
                    RoleId = 3,
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(nonMember);

                await context.SaveChangesAsync();

                teamId = team.Id;
                nonMemberUserId = nonMember.Id;
            }

            // Act - Set non-member as owner
            var response = await client.PostAsJsonAsync($"/api/teams/{teamId}/owner", nonMemberUserId);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetTeams_AsSuperadmin_ShouldSeeAllTeams()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var teamName = $"Superadmin View Team {Guid.NewGuid()}";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Teams.Add(new Team { Name = teamName });
                await context.SaveChangesAsync();
            }

            // Act
            var response = await client.GetAsync("/api/teams");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TeamResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().Contain(t => t.Name == teamName);
        }

        [Fact]
        public async Task GetTeams_AsAdmin_ShouldOnlySeeOwnAndDescendantTeams()
        {
            // Arrange
            var superadminToken = await GetSuperAdminToken();
            var superadminClient = _factory.Client;
            superadminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superadminToken);

            var adminEmail = $"admin_hier_{Guid.NewGuid().ToString()[..8]}@test.com";
            var childEmail = $"child_hier_{Guid.NewGuid().ToString()[..8]}@test.com";
            var unrelatedEmail = $"unrelated_hier_{Guid.NewGuid().ToString()[..8]}@test.com";
            var password = "Password123!";

            User adminUser;
            User childUser;
            User unrelatedUser;

            Team teamAdmin;
            Team teamChild;
            Team teamUnrelated;
            Team teamNoOwner;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                // Create Admin User
                adminUser = new User
                {
                    Name = "Hierarchical Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 2, // Admin
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                // Create Child User under Admin
                childUser = new User
                {
                    Name = "Hierarchical Child",
                    Email = childEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 3, // User
                    ParentUserId = adminUser.Id
                };
                context.Users.Add(childUser);

                // Create Unrelated User under Superadmin
                unrelatedUser = new User
                {
                    Name = "Unrelated User",
                    Email = unrelatedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 3, // User
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(unrelatedUser);
                await context.SaveChangesAsync();

                // Create teams with correct membership + ownership
                teamAdmin = await CreateTeamWithOwner(context, $"Team Owned by Admin {Guid.NewGuid()}", adminUser);
                teamChild = await CreateTeamWithOwner(context, $"Team Owned by Child {Guid.NewGuid()}", childUser);
                teamUnrelated = await CreateTeamWithOwner(context, $"Team Owned by Unrelated {Guid.NewGuid()}", unrelatedUser);

                // Create a team with no owner
                teamNoOwner = new Team { Name = $"Team with No Owner {Guid.NewGuid()}" };
                context.Teams.Add(teamNoOwner);
                await context.SaveChangesAsync();
            }

            // Act - Log in as the new Hierarchical Admin and request teams
            var adminToken = await GetTokenForUser(adminEmail, password);
            var adminClient = _factory.Client;
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var response = await adminClient.GetAsync("/api/teams");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TeamResponse>>>();
            result!.Success.Should().BeTrue();

            var visibleTeamIds = result.Data!.Select(t => t.Id).ToList();

            // 1. Should see team owned by themselves
            visibleTeamIds.Should().Contain(teamAdmin.Id);

            // 2. Should see team owned by their child
            visibleTeamIds.Should().Contain(teamChild.Id);

            // 3. Should NOT see team owned by unrelated user
            visibleTeamIds.Should().NotContain(teamUnrelated.Id);

            // 4. Should NOT see team without an owner
            visibleTeamIds.Should().NotContain(teamNoOwner.Id);
        }

        [Fact]
        public async Task CreateTeam_AsAdmin_ShouldReturnForbidden()
        {
            // Arrange
            var adminEmail = $"admin_{Guid.NewGuid().ToString()[..8]}@test.com";
            var password = "Password123!";
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                
                var adminUser = new User
                {
                    Name = "Test Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 2, // Admin
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                // Grant teams:manage permission to Admin role (if not already done)
                var permission = await context.Permissions.FirstOrDefaultAsync(p => p.Name == "teams:manage");
                if (permission != null)
                {
                    var hasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == 2 && rp.PermissionId == permission.Id);
                    if (!hasPerm)
                    {
                        context.RolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = permission.Id });
                        await context.SaveChangesAsync();
                        
                        // Clear the RBAC cache to pick up changes
                        var rbacCache = scope.ServiceProvider.GetRequiredService<IRbacCache>();
                        var updatedPerms = await context.RolePermissions
                            .Include(rp => rp.Permission)
                            .Where(rp => rp.RoleId == 2)
                            .Select(rp => rp.Permission!.Name)
                            .ToListAsync();
                        rbacCache.UpdateRolePermissions(2, updatedPerms.ToHashSet());
                    }
                }
            }

            var adminToken = await GetTokenForUser(adminEmail, password);
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var request = new CreateTeamRequest
            {
                Name = $"Admin Team {Guid.NewGuid()}",
                Members = new List<TeamMemberRequest>()
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task DeleteTeam_AsAdmin_ShouldReturnForbidden()
        {
            // Arrange
            var adminEmail = $"admin_{Guid.NewGuid().ToString()[..8]}@test.com";
            var password = "Password123!";
            int teamId;
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                
                var adminUser = new User
                {
                    Name = "Test Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 2, // Admin
                    ParentUserId = superadmin.Id
                };
                context.Users.Add(adminUser);
                
                var team = new Team { Name = $"Admin Delete Team {Guid.NewGuid()}" };
                context.Teams.Add(team);
                
                await context.SaveChangesAsync();
                teamId = team.Id;

                // Grant teams:manage permission to Admin role (if not already done)
                var permission = await context.Permissions.FirstOrDefaultAsync(p => p.Name == "teams:manage");
                if (permission != null)
                {
                    var hasPerm = await context.RolePermissions.AnyAsync(rp => rp.RoleId == 2 && rp.PermissionId == permission.Id);
                    if (!hasPerm)
                    {
                        context.RolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = permission.Id });
                        await context.SaveChangesAsync();
                        
                        // Clear the RBAC cache to pick up changes
                        var rbacCache = scope.ServiceProvider.GetRequiredService<IRbacCache>();
                        var updatedPerms = await context.RolePermissions
                            .Include(rp => rp.Permission)
                            .Where(rp => rp.RoleId == 2)
                            .Select(rp => rp.Permission!.Name)
                            .ToListAsync();
                        rbacCache.UpdateRolePermissions(2, updatedPerms.ToHashSet());
                    }
                }
            }

            var adminToken = await GetTokenForUser(adminEmail, password);
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // Act
            var response = await client.DeleteAsync($"/api/teams/{teamId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ─── Usuários Disponíveis (GetUsers) Integration Tests ─────────────────
        // These tests verify GET /api/users — the common function that powers the
        // "Usuários Disponíveis" left-column in the Equipe (Team) management modal.

        [Fact]
        public async Task GetUsers_AsSuperadmin_ShouldReturnAllActiveUsers()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var activeEmail1 = $"active1_{Guid.NewGuid().ToString()[..8]}@test.com";
            var activeEmail2 = $"active2_{Guid.NewGuid().ToString()[..8]}@test.com";
            var inactiveEmail = $"inactive_{Guid.NewGuid().ToString()[..8]}@test.com";

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                context.Users.AddRange(
                    new User { Name = "Active User 1", Email = activeEmail1, PasswordHash = "h", RoleId = 3, ParentUserId = superadmin.Id, IsActive = true },
                    new User { Name = "Active User 2", Email = activeEmail2, PasswordHash = "h", RoleId = 3, ParentUserId = superadmin.Id, IsActive = true },
                    new User { Name = "Inactive User", Email = inactiveEmail, PasswordHash = "h", RoleId = 3, ParentUserId = superadmin.Id, IsActive = false }
                );
                await context.SaveChangesAsync();
            }

            // Act — fetch with activeOnly=true (same call as Equipe modal uses)
            var response = await client.GetAsync("/api/users?page=1&pageSize=1000&activeOnly=true");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result!.Success.Should().BeTrue();

            var emails = result.Data!.Items.Select(u => u.Email).ToList();
            emails.Should().Contain(activeEmail1, "active user 1 must appear for superadmin");
            emails.Should().Contain(activeEmail2, "active user 2 must appear for superadmin");
            emails.Should().NotContain(inactiveEmail, "inactive user must be excluded when activeOnly=true");
        }

        [Fact]
        public async Task GetUsers_WithoutActiveOnlyFilter_ShouldIncludeInactiveUsers()
        {
            // Arrange — proves Bug 2: without activeOnly the API returns inactive users,
            // which waste pagination slots and can crowd out valid users.
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var inactiveEmail = $"inactive_slot_{Guid.NewGuid().ToString()[..8]}@test.com";

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                context.Users.Add(new User
                {
                    Name = "Slot-Wasting Inactive",
                    Email = inactiveEmail,
                    PasswordHash = "h",
                    RoleId = 3,
                    ParentUserId = superadmin.Id,
                    IsActive = false
                });
                await context.SaveChangesAsync();
            }

            // Act — fetch WITH status=all
            var response = await client.GetAsync("/api/users?page=1&pageSize=1000&status=all");

            // Assert — inactive users ARE returned when status=all
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result!.Success.Should().BeTrue();
            result.Data!.Items.Should().Contain(u => u.Email == inactiveEmail,
                "with status=all the API returns inactive users");
        }

        [Fact]
        public async Task GetUsers_PaginationTruncation_TotalCountExceedsReturnedItems()
        {
            // Arrange — proves Bug 1: pageSize truncation causes silent data loss.
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Seed 3 extra active users so we know at least 3 exist
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                for (int i = 0; i < 3; i++)
                {
                    context.Users.Add(new User
                    {
                        Name = $"Trunc User {i}",
                        Email = $"trunc_{Guid.NewGuid().ToString()[..8]}@test.com",
                        PasswordHash = "h",
                        RoleId = 3,
                        ParentUserId = superadmin.Id,
                        IsActive = true
                    });
                }
                await context.SaveChangesAsync();
            }

            // Act — very small pageSize to simulate truncation
            var response = await client.GetAsync("/api/users?page=1&pageSize=2&activeOnly=true");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result!.Success.Should().BeTrue();

            // TotalCount must be greater than the items returned — proves truncation occurs
            result.Data!.TotalCount.Should().BeGreaterThan(result.Data.Items.Count,
                "when pageSize < totalCount the response silently drops users beyond the page boundary");
            result.Data.Items.Should().HaveCount(2, "pageSize=2 must cap the result at 2 items");
        }

        [Fact]
        public async Task GetUsers_AsAdmin_DirectChildrenAppearWithCorrectParentUserId()
        {
            // Arrange — proves Bug 3 is avoidable: when there is no truncation the
            // client-side BFS in TeamMembersModal can correctly resolve first-children
            // because their parentUserId is present in allUsers.
            var password = "Password123!";
            var adminEmail = $"admin_pool_{Guid.NewGuid().ToString()[..8]}@test.com";
            var child1Email = $"child1_pool_{Guid.NewGuid().ToString()[..8]}@test.com";
            var child2Email = $"child2_pool_{Guid.NewGuid().ToString()[..8]}@test.com";
            var unrelatedEmail = $"unrel_pool_{Guid.NewGuid().ToString()[..8]}@test.com";

            Guid adminId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                var admin = new User
                {
                    Name = "Pool Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = 2,
                    ParentUserId = superadmin.Id,
                    IsActive = true
                };
                context.Users.Add(admin);
                await context.SaveChangesAsync();
                adminId = admin.Id;

                context.Users.AddRange(
                    new User { Name = "Child One", Email = child1Email, PasswordHash = "h", RoleId = 3, ParentUserId = adminId, IsActive = true },
                    new User { Name = "Child Two", Email = child2Email, PasswordHash = "h", RoleId = 3, ParentUserId = adminId, IsActive = true },
                    new User { Name = "Unrelated User", Email = unrelatedEmail, PasswordHash = "h", RoleId = 3, ParentUserId = superadmin.Id, IsActive = true }
                );
                await context.SaveChangesAsync();
            }

            // Act — call as admin, large pageSize to avoid truncation
            var adminToken = await GetTokenForUser(adminEmail, password);
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var response = await client.GetAsync("/api/users?page=1&pageSize=1000&activeOnly=true");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result!.Success.Should().BeTrue();

            var items = result.Data!.Items;

            // Both direct children must appear so the client BFS can find them
            var child1 = items.FirstOrDefault(u => u.Email == child1Email);
            var child2 = items.FirstOrDefault(u => u.Email == child2Email);

            child1.Should().NotBeNull("admin's first child must appear in the user pool");
            child2.Should().NotBeNull("admin's second child must appear in the user pool");

            // parentUserId must be set correctly so BFS resolves them as children
            child1!.ParentUserId.Should().Be(adminId,
                "child's parentUserId must match admin's Id for client BFS to work");
            child2!.ParentUserId.Should().Be(adminId,
                "child's parentUserId must match admin's Id for client BFS to work");
        }

        private async Task<Team> CreateTeamWithOwner(AppDbContext context, string teamName, User owner)
        {
            var team = new Team { Name = teamName };
            context.Teams.Add(team);
            await context.SaveChangesAsync();

            // Owners must be members to satisfy API constraints
            context.UserTeams.Add(new UserTeam
            {
                TeamId = team.Id,
                UserInternalId = owner.InternalId,
                StartDate = DateTime.UtcNow.AddYears(-8)
            });
            await context.SaveChangesAsync();

            team.OwnerUserInternalId = owner.InternalId;
            context.Teams.Update(team);
            await context.SaveChangesAsync();

            return team;
        }

        private async Task<string> GetTokenForUser(string email, string password)
        {
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception($"Login failed for {email}");
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"SuperAdmin login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token from login response");
        }
    }
}
