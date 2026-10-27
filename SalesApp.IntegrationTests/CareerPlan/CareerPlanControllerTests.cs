using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.CareerPlan;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.CareerPlan
{
    [Collection("Misc Tests")]
    public class CareerPlanControllerTests
    {
        private readonly HttpClient _client;
        private readonly MiscTestFactory _factory;

        public CareerPlanControllerTests(MiscTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task GetConfig_WithoutAuth_ReturnsUnauthorized()
        {
            var unauthClient = _factory.CreateClient();
            unauthClient.DefaultRequestHeaders.Authorization = null;
            var response = await unauthClient.GetAsync("/api/career-plan/config");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetConfig_AsAdmin_ReturnsForbidden()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.GetAsync("/api/career-plan/config");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetConfig_AsSuperAdmin_ReturnsConfigSummary()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.GetAsync("/api/career-plan/config");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanConfigResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.ClosingDay.Should().Be(15);
            result.Data.ConfiguredLevels.Should().Contain(new[] { "Alef", "Beth", "Guimel" });
        }

        [Fact]
        public async Task EvaluateUser_AsSuperAdmin_EvaluatesUserSuccessfully()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Fetch users list to get an existing user's ID
            var usersResponse = await clientWithToken.GetAsync("/api/users?pageSize=1");
            usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            var user = usersResult!.Data!.Items[0];

            var evalResponse = await clientWithToken.GetAsync($"/api/career-plan/users/{user.Id}");
            evalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var evalResult = await evalResponse.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            evalResult.Should().NotBeNull();
            evalResult!.Success.Should().BeTrue();
            evalResult.Data.Should().NotBeNull();
            evalResult.Data!.UserId.Should().Be(user.Id);
            evalResult.Data.ClosingDay.Should().Be(15);
            evalResult.Data.RawMetrics.Should().NotBeNull();
        }

        [Fact]
        public async Task EvaluateUser_NonExistentUser_ReturnsNotFound()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var randomId = Guid.NewGuid();
            var evalResponse = await clientWithToken.GetAsync($"/api/career-plan/users/{randomId}");
            evalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EvaluateUser_BethToGuimel_StrictRetentionExcludesContractsUnderTwoMonths()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var bethLevel = await context.ClassificationLevels.FirstAsync(l => l.Name.ToLower() == "beth");
            var activeStatus = await context.ContractStatuses.FirstAsync(s => s.Name == "Active");
            var defaultedStatus = await context.ContractStatuses.FirstAsync(s => s.Name == "Defaulted");

            // 1. Create a test user with current level = Beth
            var testUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test Beth User",
                Email = $"beth_user_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                RoleId = 3,
                IsActive = true
            };
            context.Users.Add(testUser);
            await context.SaveChangesAsync();

            context.UserClassifications.Add(new UserClassification
            {
                UserInternalId = testUser.InternalId,
                LevelId = bethLevel.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6)
            });
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var refDate = CareerReferenceDate.Resolve(now, 15);

            // Contract 1: 4 months old (> 2 months), Active, R$ 10.000.000,00
            var contract1 = new Contract
            {
                ContractNumber = $"CONT-BETH-1-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 10_000_000m,
                ContractStatusId = activeStatus.Id,
                SaleStartDate = refDate.AddMonths(-4),
                IsActive = true,
                HasPayment = true
            };

            // Contract 2: 1 month old (<= 2 months), Defaulted (loss), R$ 5.000.000,00
            // Because minAgeMonths is 2, this contract MUST be excluded from Guimel's strict retention requirement!
            var contract2 = new Contract
            {
                ContractNumber = $"CONT-BETH-2-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 5_000_000m,
                ContractStatusId = defaultedStatus.Id,
                SaleStartDate = refDate.AddMonths(-1),
                IsActive = true,
                HasPayment = false
            };

            context.Contracts.AddRange(contract1, contract2);
            await context.SaveChangesAsync();

            // 2. Evaluate user via API
            var evalResponse = await clientWithToken.GetAsync($"/api/career-plan/users/{testUser.Id}");
            evalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var evalResult = await evalResponse.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            evalResult.Should().NotBeNull();
            evalResult!.Success.Should().BeTrue();
            evalResult.Data!.CurrentLevel!.Name.Should().Be("Beth");
            evalResult.Data.TargetLevel!.Name.Should().Be("Guimel");

            // Strict retention for Guimel: only contract 1 qualifies (100% retention >= 79%)
            var retentionReq = evalResult.Data.Requirements.First(r => r.Metric == "StrictRetention");
            retentionReq.Passed.Should().BeTrue("contract with <= 2 months is excluded, leaving 100% retention on eligible contracts");
            retentionReq.CurrentValue.Should().Be(1m);
            retentionReq.TargetValue.Should().Be(0.79m);
            retentionReq.Progress.Should().Be(1m);

            // Production: contract 1 (10M) + contract 2 (5M) = 15M >= 10M meta
            var prodReq = evalResult.Data.Requirements.First(r => r.Metric == "Production");
            prodReq.Passed.Should().BeTrue();
            prodReq.CurrentValue.Should().Be(15_000_000m);
            prodReq.Progress.Should().Be(1m);

            // 3. Now add Contract 3: 5 months old (> 2 months), Defaulted, R$ 10.000.000,00
            // This should drop strict retention on eligible contracts to 10M / 20M = 50% (< 79%), failing the requirement.
            var contract3 = new Contract
            {
                ContractNumber = $"CONT-BETH-3-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 10_000_000m,
                ContractStatusId = defaultedStatus.Id,
                SaleStartDate = refDate.AddMonths(-5),
                IsActive = true,
                HasPayment = false
            };
            context.Contracts.Add(contract3);
            await context.SaveChangesAsync();

            var evalResponse2 = await clientWithToken.GetAsync($"/api/career-plan/users/{testUser.Id}");
            evalResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
            var evalResult2 = await evalResponse2.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();

            var retentionReq2 = evalResult2!.Data!.Requirements.First(r => r.Metric == "StrictRetention");
            retentionReq2.Passed.Should().BeFalse("strict retention dropped to 50% which is below 79%");
            retentionReq2.CurrentValue.Should().Be(0.5m);
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
