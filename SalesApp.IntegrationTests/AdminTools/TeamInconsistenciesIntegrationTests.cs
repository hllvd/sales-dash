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
using SalesApp.IntegrationTests.Contracts;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.AdminTools
{
    [Collection("Contracts Tests")]
    public class TeamInconsistenciesIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly ContractsTestFactory _factory;

        public TeamInconsistenciesIntegrationTests(ContractsTestFactory factory)
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

        [Fact]
        public async Task GetTeamInconsistencies_ShouldDetectOwnerWithoutActiveMembership()
        {
            var token = await GetSuperAdminTokenAsync();
            var unique = Guid.NewGuid().ToString().Substring(0, 8);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Create a user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = $"Owner Ghost {unique}",
                Email = $"ghost_{unique}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3,
                IsActive = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // 2. Create a team where this user is owner, but user has NO active membership in it
            var team = new Team
            {
                Name = $"Team Ghost {unique}",
                OwnerUserInternalId = user.InternalId,
                IsActive = true
            };
            context.Teams.Add(team);
            await context.SaveChangesAsync();

            // Add an expired membership in that team
            var expiredMembership = new UserTeam
            {
                TeamId = team.Id,
                UserInternalId = user.InternalId,
                StartDate = DateTime.UtcNow.AddMonths(-12),
                EndDate = DateTime.UtcNow.AddMonths(-2), // expired!
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.UserTeams.Add(expiredMembership);
            await context.SaveChangesAsync();

            // Request inconsistencies
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin-tools/team-inconsistencies");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var res = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminTeamInconsistencyItemDto>>>();
            res.Should().NotBeNull();
            res!.Success.Should().BeTrue();

            var detected = res.Data.FirstOrDefault(i => i.UserId == user.Id);
            detected.Should().NotBeNull();
            detected!.OwnedTeamId.Should().Be(team.Id);
            detected.Inconsistencies.Should().Contain(i => i.Type == "OwnerWithoutActiveMembership");
            detected.Recommendation.Should().NotBeNull();
            detected.Recommendation!.SuggestedTeamId.Should().Be(team.Id);
            detected.Recommendation.SuggestSetAsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task MigrateTeamMember_ShouldResolveInconsistencyAndCreateActiveMembership()
        {
            var token = await GetSuperAdminTokenAsync();
            var unique = Guid.NewGuid().ToString().Substring(0, 8);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = $"Migrate Target {unique}",
                Email = $"migrate_{unique}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3,
                IsActive = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var team = new Team
            {
                Name = $"Target Team {unique}",
                OwnerUserInternalId = user.InternalId,
                IsActive = true
            };
            context.Teams.Add(team);
            await context.SaveChangesAsync();

            // Perform migration via endpoint
            var migrateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin-tools/team-inconsistencies/migrate");
            migrateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            migrateRequest.Content = JsonContent.Create(new AdminMigrateTeamMemberRequest
            {
                UserId = user.Id,
                TargetTeamId = team.Id,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                SetAsOwner = true,
                CloseConflictingPeriods = true
            });

            var response = await _client.SendAsync(migrateRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var res = await response.Content.ReadFromJsonAsync<ApiResponse<AdminMigrateTeamMemberResult>>();
            res.Should().NotBeNull();
            res!.Success.Should().BeTrue();
            res.Data.TargetTeamId.Should().Be(team.Id);

            // Verify in DB that user now has an active membership in that team
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeMembership = await verifyContext.UserTeams
                .FirstOrDefaultAsync(ut => ut.UserInternalId == user.InternalId && ut.TeamId == team.Id && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow));
            activeMembership.Should().NotBeNull();
        }

        [Fact]
        public async Task ExportTeamInconsistenciesXlsx_ShouldReturnValidSpreadsheetBytes()
        {
            var token = await GetSuperAdminTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin-tools/team-inconsistencies/export-xlsx");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Length.Should().BeGreaterThan(100);
        }
    }
}
