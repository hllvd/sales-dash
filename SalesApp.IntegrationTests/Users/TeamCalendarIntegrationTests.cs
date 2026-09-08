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
    [Collection("Users Tests")]
    public class TeamCalendarIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly UsersTestFactory _factory;

        public TeamCalendarIntegrationTests(UsersTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task GetTeamCalendar_AsSuperAdmin_ShouldReturnUsersWithHierarchyLevelsAndHistory()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/api/teams/calendar");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TeamCalendarUserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // All returned users must have hierarchyLevel between 1 and 3
            foreach (var user in result.Data!)
            {
                user.HierarchyLevel.Should().BeInRange(1, 3);
                user.UserId.Should().NotBeEmpty();
                user.UserName.Should().NotBeNullOrWhiteSpace();
            }
        }

        [Fact]
        public async Task GetContractPreview_ShouldReturnOlderAndNewerContractsProperlySorted()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a test user with contracts around a boundary date
            Guid testUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Preview Test User {Guid.NewGuid():N}",
                    Email = $"preview.test.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
                testUserId = testUser.Id;

                var boundary = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

                // Add 3 contracts before boundary
                for (int i = 1; i <= 3; i++)
                {
                    context.Contracts.Add(new Contract
                    {
                        ContractNumber = $"PREV-OLD-{Guid.NewGuid():N}",
                        UserInternalId = testUser.InternalId,
                        TotalAmount = 1000 * i,
                        ContractStatusId = 1,
                        SaleStartDate = boundary.AddDays(-i * 5),
                        CustomerName = $"Older Client {i}",
                        IsActive = true
                    });
                }

                // Add 3 contracts on or after boundary
                for (int i = 1; i <= 3; i++)
                {
                    context.Contracts.Add(new Contract
                    {
                        ContractNumber = $"PREV-NEW-{Guid.NewGuid():N}",
                        UserInternalId = testUser.InternalId,
                        TotalAmount = 2000 * i,
                        ContractStatusId = 1,
                        SaleStartDate = boundary.AddDays((i - 1) * 5),
                        CustomerName = $"Newer Client {i}",
                        IsActive = true
                    });
                }

                await context.SaveChangesAsync();
            }

            var boundaryDateStr = "2025-06-15T00:00:00Z";

            // Act
            var response = await client.GetAsync($"/api/teams/calendar/contract-preview?userId={testUserId}&boundaryDate={boundaryDateStr}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CalendarContractPreviewResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Verify older contracts (SaleStartDate < boundary, descending order)
            result.Data!.OlderTeamContracts.Should().NotBeEmpty();
            result.Data.OlderTeamContracts.Count.Should().BeLessOrEqualTo(5);
            for (int i = 0; i < result.Data.OlderTeamContracts.Count - 1; i++)
            {
                result.Data.OlderTeamContracts[i].SaleStartDate
                    .Should().BeOnOrAfter(result.Data.OlderTeamContracts[i + 1].SaleStartDate);
            }

            // Verify newer contracts (SaleStartDate >= boundary, ascending order)
            result.Data!.NewerTeamContracts.Should().NotBeEmpty();
            result.Data.NewerTeamContracts.Count.Should().BeLessOrEqualTo(5);
            for (int i = 0; i < result.Data.NewerTeamContracts.Count - 1; i++)
            {
                result.Data.NewerTeamContracts[i].SaleStartDate
                    .Should().BeOnOrBefore(result.Data.NewerTeamContracts[i + 1].SaleStartDate);
            }
        }

        [Fact]
        public async Task AdjustTeamBoundary_WithValidDates_ShouldUpdateDatesSuccessfully()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamAId;
            int teamBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Boundary User {Guid.NewGuid():N}",
                    Email = $"boundary.user.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var teamA = new Team { Name = $"Team A {Guid.NewGuid():N}" };
                var teamB = new Team { Name = $"Team B {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamA, teamB);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamAId = teamA.Id;
                teamBId = teamB.Id;

                // Initial periods: Team A (2024-01-01 to 2024-06-01), Team B (2024-06-01 to null)
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamAId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                });
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamBId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            // Adjust boundary to 2024-07-01 (more than 7 days from start of Team A)
            var request = new AdjustTeamBoundaryRequest
            {
                UserId = testUserId,
                OlderTeamId = teamAId,
                NewerTeamId = teamBId,
                BoundaryDate = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var response = await client.PutAsJsonAsync("/api/teams/calendar/adjust-boundary", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamCalendarUserResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();

            // Verify in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var utA = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamAId && ut.User.Id == testUserId);
                var utB = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamBId && ut.User.Id == testUserId);

                utA.Should().NotBeNull();
                utB.Should().NotBeNull();
                utA!.EndDate.Should().Be(new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc));
                utB!.StartDate.Should().Be(new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc));
            }
        }

        [Fact]
        public async Task AdjustTeamBoundary_WithLessThan7DaysDuration_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamAId;
            int teamBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Short Duration User {Guid.NewGuid():N}",
                    Email = $"short.duration.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var teamA = new Team { Name = $"Short Team A {Guid.NewGuid():N}" };
                var teamB = new Team { Name = $"Short Team B {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamA, teamB);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamAId = teamA.Id;
                teamBId = teamB.Id;

                // Team A starts on 2024-01-01
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamAId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                });
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamBId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            // Adjust boundary to 2024-01-03 (only 2 days duration for Team A -> less than 7 days!)
            var request = new AdjustTeamBoundaryRequest
            {
                UserId = testUserId,
                OlderTeamId = teamAId,
                NewerTeamId = teamBId,
                BoundaryDate = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var response = await client.PutAsJsonAsync("/api/teams/calendar/adjust-boundary", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamCalendarUserResponse>>();
            result!.Message.Should().Contain("1 semana");
        }

        [Fact]
        public async Task UpdateMemberDates_WithLessThan7DaysDuration_ShouldReturnBadRequest()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Member Dates User {Guid.NewGuid():N}",
                    Email = $"member.dates.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var team = new Team { Name = $"Dates Team {Guid.NewGuid():N}" };
                context.Teams.Add(team);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamId = team.Id;

                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            var request = new UpdateMemberDatesRequest
            {
                StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc) // only 3 days
            };

            // Act
            var response = await client.PutAsJsonAsync($"/api/teams/{teamId}/members/{testUserId}", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>();
            result!.Message.Should().Contain("1 semana");
        }

        [Fact]
        public async Task GetAvailableTeams_ShouldReturnTeamsList()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/api/teams/calendar/available-teams");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<AvailableTeamItemResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task AssignUserTeam_ShouldClosePreviousAndCreateNewMembership()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamOldId;
            int teamNewId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Wizard Assign User {Guid.NewGuid():N}",
                    Email = $"wizard.assign.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var teamOld = new Team { Name = $"Old Team {Guid.NewGuid():N}" };
                var teamNew = new Team { Name = $"New Team {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamOld, teamNew);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamOldId = teamOld.Id;
                teamNewId = teamNew.Id;

                // Old team active from 2024-01-01
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamOldId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            var request = new AssignUserTeamRequest
            {
                UserId = testUserId,
                NewTeamId = teamNewId,
                StartDate = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams/calendar/assign-team", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TeamCalendarUserResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Verify in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var oldMembership = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamOldId && ut.User.Id == testUserId);
                var newMembership = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamNewId && ut.User.Id == testUserId);

                oldMembership.Should().NotBeNull();
                newMembership.Should().NotBeNull();
                oldMembership!.EndDate.Should().Be(new DateTime(2024, 7, 31, 0, 0, 0, DateTimeKind.Utc));
                newMembership!.StartDate.Should().Be(new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc));
                newMembership.EndDate.Should().BeNull();
            }
        }

        [Fact]
        public async Task AssignFirstTeam_WithExistingContracts_ShouldAdjustStartDateToOneDayBeforeEarliestContract()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamId;
            var contractDate = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"First Team Contract User {Guid.NewGuid():N}",
                    Email = $"first.team.contract.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var team = new Team { Name = $"Contract Team {Guid.NewGuid():N}" };
                context.Teams.Add(team);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamId = team.Id;

                // Add contract
                context.Contracts.Add(new Contract
                {
                    ContractNumber = $"FIRST-CTR-{Guid.NewGuid():N}",
                    UserInternalId = testUser.InternalId,
                    TotalAmount = 5000,
                    ContractStatusId = 1,
                    SaleStartDate = contractDate,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            // Attempt to assign team starting AFTER the contract
            var request = new AssignUserTeamRequest
            {
                UserId = testUserId,
                NewTeamId = teamId,
                StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams/calendar/assign-team", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify in DB that start date was adjusted to 1 day before the contract (2024-05-14)
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var membership = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamId && ut.User.Id == testUserId);
                membership.Should().NotBeNull();
                membership!.StartDate.Should().Be(new DateTime(2024, 5, 14, 0, 0, 0, DateTimeKind.Utc));
            }
        }

        [Fact]
        public async Task AssignUserTeam_WithUpdateParentUser_ShouldUpdateParentUserIdToTeamOwner()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            Guid ownerUserId;
            int teamId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ownerUser = new User
                {
                    Name = $"Team Owner {Guid.NewGuid():N}",
                    Email = $"team.owner.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                var memberUser = new User
                {
                    Name = $"Member User {Guid.NewGuid():N}",
                    Email = $"member.user.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 3,
                    IsActive = true
                };
                context.Users.AddRange(ownerUser, memberUser);
                await context.SaveChangesAsync();

                var team = new Team
                {
                    Name = $"Owned Team {Guid.NewGuid():N}",
                    OwnerUserInternalId = ownerUser.InternalId
                };
                context.Teams.Add(team);
                await context.SaveChangesAsync();

                testUserId = memberUser.Id;
                ownerUserId = ownerUser.Id;
                teamId = team.Id;
            }

            var request = new AssignUserTeamRequest
            {
                UserId = testUserId,
                NewTeamId = teamId,
                StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdateParentUser = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams/calendar/assign-team", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify in DB that Member's ParentUserId was updated to ownerUserId
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var memberInDb = await context.Users.FirstOrDefaultAsync(u => u.Id == testUserId);
                memberInDb.Should().NotBeNull();
                memberInDb!.ParentUserId.Should().Be(ownerUserId);
            }
        }

        [Fact]
        public async Task AssignUserTeam_WithCircularParent_ShouldPreventCircularHierarchy()
        {
            // Arrange: Setup hierarchy: grandParent -> childUser (childUser has grandParent as ancestor)
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid grandParentId;
            Guid childId;
            int teamId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var grandParent = new User
                {
                    Name = $"GrandParent {Guid.NewGuid():N}",
                    Email = $"grandparent.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true,
                    ParentUserId = null
                };
                context.Users.Add(grandParent);
                await context.SaveChangesAsync();

                var childUser = new User
                {
                    Name = $"Subordinate Child {Guid.NewGuid():N}",
                    Email = $"child.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true,
                    ParentUserId = grandParent.Id
                };
                context.Users.Add(childUser);
                await context.SaveChangesAsync();

                var team = new Team
                {
                    Name = $"Child Owned Team {Guid.NewGuid():N}",
                    OwnerUserInternalId = childUser.InternalId
                };
                context.Teams.Add(team);
                await context.SaveChangesAsync();

                grandParentId = grandParent.Id;
                childId = childUser.Id;
                teamId = team.Id;
            }

            // Attempt to assign grandParent to childUser's team with UpdateParentUser = true
            // Setting grandParent's parent to childUser would create a cycle: childUser -> grandParent -> childUser
            var request = new AssignUserTeamRequest
            {
                UserId = grandParentId,
                NewTeamId = teamId,
                StartDate = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdateParentUser = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/teams/calendar/assign-team", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify in DB that grandParent's ParentUserId remains null (cycle prevented)
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var grandParentInDb = await context.Users.FirstOrDefaultAsync(u => u.Id == grandParentId);
                grandParentInDb.Should().NotBeNull();
                grandParentInDb!.ParentUserId.Should().BeNull();
            }
        }

        [Fact]
        public async Task UpdateMemberDates_ShouldSeamlesslySyncNeighborBoundaries_WithZeroGapAndZeroOverlap()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamAId;
            int teamBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Name = $"Continuity User {Guid.NewGuid():N}",
                    Email = $"continuity.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hashedpassword",
                    RoleId = 2,
                    IsActive = true
                };
                context.Users.Add(testUser);

                var teamA = new Team { Name = $"Cont Team A {Guid.NewGuid():N}" };
                var teamB = new Team { Name = $"Cont Team B {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamA, teamB);
                await context.SaveChangesAsync();

                testUserId = testUser.Id;
                teamAId = teamA.Id;
                teamBId = teamB.Id;

                // Team A: 2024-01-01 to 2024-05-31
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamAId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 5, 31, 0, 0, 0, DateTimeKind.Utc)
                });

                // Team B: 2024-06-01 to null
                context.UserTeams.Add(new UserTeam
                {
                    TeamId = teamBId,
                    UserInternalId = testUser.InternalId,
                    StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                });
                await context.SaveChangesAsync();
            }

            // Act: Update Team B's StartDate to 2024-06-15
            var updateRequest = new UpdateMemberDatesRequest
            {
                StartDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = null
            };
            var response = await client.PutAsJsonAsync($"/api/teams/{teamBId}/members/{testUserId}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var utA = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamAId && ut.User.Id == testUserId);
                var utB = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamBId && ut.User.Id == testUserId);

                utA.Should().NotBeNull();
                utB.Should().NotBeNull();

                // Team A EndDate synced to 2024-06-14 (1 day before Team B StartDate)
                utA!.EndDate.Should().Be(new DateTime(2024, 6, 14, 0, 0, 0, DateTimeKind.Utc));
                utB!.StartDate.Should().Be(new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));
            }
        }

        [Fact]
        public async Task DeleteMemberPeriod_WhenInMiddle_ShouldBridgeGapBetweenPrecedingAndSucceeding()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamAId, teamBId, teamCId;
            int userTeamBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Id = Guid.NewGuid(),
                    Name = "User Delete Mid",
                    Email = $"user.del.mid.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hash",
                    RoleId = 3,
                    IsActive = true
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
                testUserId = testUser.Id;

                var teamA = new Team { Name = $"Team Del A {Guid.NewGuid():N}" };
                var teamB = new Team { Name = $"Team Del B {Guid.NewGuid():N}" };
                var teamC = new Team { Name = $"Team Del C {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamA, teamB, teamC);
                await context.SaveChangesAsync();

                teamAId = teamA.Id;
                teamBId = teamB.Id;
                teamCId = teamC.Id;

                var utA = new UserTeam
                {
                    UserInternalId = testUser.InternalId,
                    TeamId = teamAId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 5, 31, 0, 0, 0, DateTimeKind.Utc)
                };
                var utB = new UserTeam
                {
                    UserInternalId = testUser.InternalId,
                    TeamId = teamBId,
                    StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc)
                };
                var utC = new UserTeam
                {
                    UserInternalId = testUser.InternalId,
                    TeamId = teamCId,
                    StartDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                };
                context.UserTeams.AddRange(utA, utB, utC);
                await context.SaveChangesAsync();

                userTeamBId = utB.Id;
            }

            // Act: Delete Team B period
            var response = await client.DeleteAsync($"/api/teams/{teamBId}/members/{testUserId}/period/{userTeamBId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var utA = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamAId && ut.User.Id == testUserId);
                var utB = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamBId && ut.User.Id == testUserId);
                var utC = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamCId && ut.User.Id == testUserId);

                utB.Should().BeNull();
                utA.Should().NotBeNull();
                utC.Should().NotBeNull();

                // Team A EndDate should now bridge to 2024-08-31 (1 day before Team C StartDate 2024-09-01)
                utA!.EndDate.Should().Be(new DateTime(2024, 8, 31, 0, 0, 0, DateTimeKind.Utc));
            }
        }

        [Fact]
        public async Task DeleteMemberPeriod_WhenActive_ShouldMakePrecedingActive()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid testUserId;
            int teamAId, teamBId;
            int userTeamBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var testUser = new User
                {
                    Id = Guid.NewGuid(),
                    Name = "User Delete Active",
                    Email = $"user.del.act.{Guid.NewGuid():N}@test.com",
                    PasswordHash = "hash",
                    RoleId = 3,
                    IsActive = true
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
                testUserId = testUser.Id;

                var teamA = new Team { Name = $"Team Act A {Guid.NewGuid():N}" };
                var teamB = new Team { Name = $"Team Act B {Guid.NewGuid():N}" };
                context.Teams.AddRange(teamA, teamB);
                await context.SaveChangesAsync();

                teamAId = teamA.Id;
                teamBId = teamB.Id;

                var utA = new UserTeam
                {
                    UserInternalId = testUser.InternalId,
                    TeamId = teamAId,
                    StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2024, 5, 31, 0, 0, 0, DateTimeKind.Utc)
                };
                var utB = new UserTeam
                {
                    UserInternalId = testUser.InternalId,
                    TeamId = teamBId,
                    StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null
                };
                context.UserTeams.AddRange(utA, utB);
                await context.SaveChangesAsync();

                userTeamBId = utB.Id;
            }

            // Act: Delete Team B (active)
            var response = await client.DeleteAsync($"/api/teams/{teamBId}/members/{testUserId}/period/{userTeamBId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var utA = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamAId && ut.User.Id == testUserId);
                var utB = await context.UserTeams.FirstOrDefaultAsync(ut => ut.TeamId == teamBId && ut.User.Id == testUserId);

                utB.Should().BeNull();
                utA.Should().NotBeNull();

                // Team A EndDate is now null (active)
                utA!.EndDate.Should().BeNull();
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
    }
}
