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
