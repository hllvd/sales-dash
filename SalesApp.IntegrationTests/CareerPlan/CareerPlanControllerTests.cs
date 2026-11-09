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
        public async Task GetConfig_AsUser_ReturnsForbidden()
        {
            var token = await GetUserToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.GetAsync("/api/career-plan/config");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetConfig_AsAdmin_ReturnsConfigSummary()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.GetAsync("/api/career-plan/config");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanConfigResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
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

            // Contract 1: 4 months old, Active, R$ 10.000.000,00 (Paid months = 4 >= 2)
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

            // Contract 2: 1 month old, Active, R$ 5.000.000,00 (Paid months = 1 < 2)
            // Because paid months < 2, this contract is excluded from ActiveProduction! But it is Active, so retention stays 100%.
            var contract2 = new Contract
            {
                ContractNumber = $"CONT-BETH-2-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 5_000_000m,
                ContractStatusId = activeStatus.Id,
                SaleStartDate = refDate.AddMonths(-1),
                IsActive = true,
                HasPayment = true
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

            // Strict retention for Guimel: both contracts are active (100% retention >= 79%)
            var retentionReq = evalResult.Data.Requirements.First(r => r.Metric == "StrictRetention");
            retentionReq.Passed.Should().BeTrue("all contracts in 15m are active, resulting in 100% strict retention");
            retentionReq.CurrentValue.Should().Be(1m);
            retentionReq.TargetValue.Should().Be(0.79m);
            retentionReq.Progress.Should().Be(1m);

            // CommercializedContracts: contract 1 qualifies (10M, 4 months old, Active, age >= 2) >= 10M meta. Contract 2 (1 month old, age < 2) is excluded.
            var prodReq = evalResult.Data.Requirements.First(r => r.Metric == "CommercializedContracts");
            prodReq.Passed.Should().BeTrue();
            prodReq.CurrentValue.Should().Be(10_000_000m);
            prodReq.Progress.Should().Be(1m);
            prodReq.RawMetricKey.Should().Be("commercializedContracts12mMinAge2");

            // CommercializedContracts12mMinAge2 in raw metrics matches
            evalResult.Data.RawMetrics.CommercializedContracts12mMinAge2.Should().Be(10_000_000m);
            evalResult.Data.RawMetrics.ActiveProduction12mMinPaid2.Should().Be(10_000_000m);

            // 3. Now add Contract 3: 5 months old, Defaulted, R$ 15.000.000,00
            // This should drop strict retention on eligible contracts (15M active / 30M total = 50% < 79%), failing retention requirement.
            var contract3 = new Contract
            {
                ContractNumber = $"CONT-BETH-3-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 15_000_000m,
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

            // Contract 3 is Defaulted with age 5 (>= 2), so CommercializedContracts includes it: 10M + 15M = 25M
            var prodReq2 = evalResult2.Data.Requirements.First(r => r.Metric == "CommercializedContracts");
            prodReq2.CurrentValue.Should().Be(25_000_000m);
            evalResult2.Data.RawMetrics.CommercializedContracts12mMinAge2.Should().Be(25_000_000m);
            evalResult2.Data.RawMetrics.ActiveProduction12mMinPaid2.Should().Be(10_000_000m);
        }

        [Fact]
        public async Task GetContractsRt_WithoutAuth_ReturnsUnauthorized()
        {
            var unauthClient = _factory.CreateClient();
            unauthClient.DefaultRequestHeaders.Authorization = null;
            var response = await unauthClient.GetAsync($"/api/career-plan/users/{Guid.NewGuid()}/contracts-rt");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetContractsRt_InvalidPagination_ReturnsBadRequest()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var res1 = await clientWithToken.GetAsync($"/api/career-plan/users/{Guid.NewGuid()}/contracts-rt?page=0");
            res1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var res2 = await clientWithToken.GetAsync($"/api/career-plan/users/{Guid.NewGuid()}/contracts-rt?pageSize=150");
            res2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetContractsRt_ForGuimelUser_ReturnsApplicableAndEvaluatedRows()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            var guimelLevel = await db.ClassificationLevels.FirstAsync(l => l.Name == "Guimel");
            var activeStatus = await db.ContractStatuses.FirstAsync(s => s.Name == "Active");
            var late1Status = await db.ContractStatuses.FirstAsync(s => s.Name == "Late1");

            var uc = new UserClassification
            {
                UserInternalId = user.InternalId,
                LevelId = guimelLevel.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                CreatedAt = DateTime.UtcNow
            };
            db.UserClassifications.Add(uc);

            var c1 = new Contract
            {
                ContractNumber = "RT-CTR-001",
                CustomerName = "Cliente RT 1",
                UserInternalId = user.InternalId,
                TotalAmount = 80_000m,
                SaleStartDate = DateTime.UtcNow.AddMonths(-4),
                ContractStatusId = activeStatus.Id,
                HasPayment = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var c2 = new Contract
            {
                ContractNumber = "RT-CTR-002",
                CustomerName = "Cliente RT 2",
                UserInternalId = user.InternalId,
                TotalAmount = 150_000m,
                SaleStartDate = DateTime.UtcNow.AddMonths(-3),
                ContractStatusId = late1Status.Id,
                HasPayment = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Contracts.AddRange(c1, c2);
            await db.SaveChangesAsync();

            var response = await clientWithToken.GetAsync($"/api/career-plan/users/{user.Id}/contracts-rt?page=1&pageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractRtPageDto>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.IsApplicable.Should().BeTrue();
            result.Data.Items.Should().NotBeEmpty();

            var row2 = result.Data.Items.FirstOrDefault(i => i.ContractNumber == "RT-CTR-002");
            row2.Should().NotBeNull();
            row2!.Status.Should().Be("Late1");
            row2.Current.Should().Be("overdue");
            row2.Rts.Should().Contain("unpaid");

            var row1 = result.Data.Items.FirstOrDefault(i => i.ContractNumber == "RT-CTR-001");
            row1.Should().NotBeNull();
            row1!.Status.Should().Be("Active");
            row1.Current.Should().Be("upToDate");
        }

        [Fact]
        public async Task GetConsultantsList_AsSuperAdmin_ReturnsConsultantsList()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.GetAsync("/api/career-plan/consultants");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanConsultantsListResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Consultants.Should().NotBeNull();
        }

        [Fact]
        public async Task RecalculateConsultants_AsSuperAdmin_RecalculatesSuccessfully()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await clientWithToken.PostAsync("/api/career-plan/consultants/recalculate", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<RecalculateCareerPlanResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Success.Should().BeTrue();
            result.Data.ProcessedCount.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task EvaluateUser_AsAdmin_CanEvaluateSelf()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid adminId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var admin = await db.Users.FirstAsync(u => u.Email == "admin@test.com");
                adminId = admin.Id;
            }

            var response = await clientWithToken.GetAsync($"/api/career-plan/users/{adminId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.UserEmail.Should().Be("admin@test.com");
        }

        [Fact]
        public async Task EvaluateUser_AsAdmin_CanEvaluateDirectSubordinate()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid childUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var admin = await db.Users.FirstAsync(u => u.Email == "admin@test.com");
                var child = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin_child@test.com");
                if (child == null)
                {
                    child = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Admin Child",
                        Email = "admin_child@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                        RoleId = 3,
                        ParentUserId = admin.Id,
                        IsActive = true
                    };
                    db.Users.Add(child);
                    await db.SaveChangesAsync();
                }
                childUserId = child.Id;
            }

            var response = await clientWithToken.GetAsync($"/api/career-plan/users/{childUserId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.UserEmail.Should().Be("admin_child@test.com");
        }

        [Fact]
        public async Task EvaluateUser_AsAdmin_CannotEvaluateNonSubordinate_ReturnsForbidden()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid otherUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await db.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                var other = await db.Users.FirstOrDefaultAsync(u => u.Email == "other_user_non_subordinate@test.com");
                if (other == null)
                {
                    other = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Other User",
                        Email = "other_user_non_subordinate@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                        RoleId = 3,
                        ParentUserId = superadmin.Id,
                        IsActive = true
                    };
                    db.Users.Add(other);
                    await db.SaveChangesAsync();
                }
                otherUserId = other.Id;
            }

            var response = await clientWithToken.GetAsync($"/api/career-plan/users/{otherUserId}");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetContractsRt_AsAdmin_CannotAccessNonSubordinate_ReturnsForbidden()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid otherUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await db.Users.FirstAsync(u => u.Email == "superadmin@test.com");
                var other = await db.Users.FirstOrDefaultAsync(u => u.Email == "other_user_non_subordinate@test.com");
                if (other == null)
                {
                    other = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Other User",
                        Email = "other_user_non_subordinate@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                        RoleId = 3,
                        ParentUserId = superadmin.Id,
                        IsActive = true
                    };
                    db.Users.Add(other);
                    await db.SaveChangesAsync();
                }
                otherUserId = other.Id;
            }

            var response = await clientWithToken.GetAsync($"/api/career-plan/users/{otherUserId}/contracts-rt");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetConsultantsList_AsAdmin_ReturnsOnlySelfAndDirectSubordinates()
        {
            var token = await GetAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            Guid adminId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var admin = await db.Users.FirstAsync(u => u.Email == "admin@test.com");
                adminId = admin.Id;
            }

            var response = await clientWithToken.GetAsync("/api/career-plan/consultants");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CareerPlanConsultantsListResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();

            // Every consultant returned MUST be either the admin or a direct subordinate
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var directSubordinateIds = (await db.Users
                    .Where(u => u.ParentUserId == adminId && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync())
                    .ToHashSet();

                foreach (var c in result.Data!.Consultants)
                {
                    (c.UserId == adminId || directSubordinateIds.Contains(c.UserId)).Should().BeTrue();
                }
            }
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token");
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

        [Fact]
        public async Task SimulateUser_SuperAdmin_RecalculatesMetricsWithoutPersisting()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var bethLevel = await context.ClassificationLevels.FirstAsync(l => l.Name.ToLower() == "beth");
            var activeStatus = await context.ContractStatuses.FirstAsync(s => s.Name == "Active");

            var testUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test Simulation User",
                Email = $"sim_user_{Guid.NewGuid().ToString()[..8]}@test.com",
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

            var now = DateTime.UtcNow;
            var refDate = CareerReferenceDate.Resolve(now, 15);

            var contract1 = new Contract
            {
                ContractNumber = $"CONT-SIM-1-{Guid.NewGuid().ToString()[..6]}",
                UserInternalId = testUser.InternalId,
                TotalAmount = 10_000_000m,
                ContractStatusId = activeStatus.Id,
                SaleStartDate = refDate.AddMonths(-4),
                IsActive = true,
                HasPayment = true
            };
            context.Contracts.Add(contract1);
            await context.SaveChangesAsync();

            // Baseline evaluation: strict retention is 100%
            var baseEval = await clientWithToken.GetAsync($"/api/career-plan/users/{testUser.Id}");
            baseEval.StatusCode.Should().Be(HttpStatusCode.OK);
            var baseResult = await baseEval.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            baseResult!.Data!.RawMetrics.StrictRetention15m.Should().Be(1m);

            // Simulation: override contract1 to overdue
            var simReq = new SimulateCareerPlanRequest
            {
                Overrides = new System.Collections.Generic.List<ContractRtOverrideDto>
                {
                    new ContractRtOverrideDto
                    {
                        ContractNumber = contract1.ContractNumber,
                        SimulatedCurrentStatus = "overdue"
                    }
                }
            };

            var simResponse = await clientWithToken.PostAsJsonAsync($"/api/career-plan/users/{testUser.Id}/simulate", simReq);
            simResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var simResult = await simResponse.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            simResult.Should().NotBeNull();
            simResult!.Success.Should().BeTrue();
            // With contract1 simulated overdue (Late1), retention drops to 0%
            simResult.Data!.RawMetrics.StrictRetention15m.Should().Be(0m);

            // Verify database was NOT changed: contract in DB remains Active
            var contractInDb = await context.Contracts.AsNoTracking().FirstAsync(c => c.ContractNumber == contract1.ContractNumber);
            contractInDb.ContractStatusId.Should().Be(activeStatus.Id);

            // Subsequent normal evaluation still returns baseline 100%
            var evalAfter = await clientWithToken.GetAsync($"/api/career-plan/users/{testUser.Id}");
            var evalAfterResult = await evalAfter.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            evalAfterResult!.Data!.RawMetrics.StrictRetention15m.Should().Be(1m);
        }

        [Fact]
        public async Task EvaluateUser_CommercializedContracts12mMinAge2_IncludesAllowedStatusesAndExcludesAgeUnderTwo()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alefLevel = await context.ClassificationLevels.FirstAsync(l => l.Name.ToLower() == "alef");
            var activeStatus = await context.ContractStatuses.FirstAsync(s => s.Name == "Active");
            var late1Status = await context.ContractStatuses.FirstAsync(s => s.Name == "Late1");
            var late2Status = await context.ContractStatuses.FirstAsync(s => s.Name == "Late2");
            var late3Status = await context.ContractStatuses.FirstAsync(s => s.Name == "Late3");
            var defaultedStatus = await context.ContractStatuses.FirstAsync(s => s.Name == "Defaulted");
            var desistenteStatus = await context.ContractStatuses.FirstOrDefaultAsync(s => s.Name == "Desistente");
            if (desistenteStatus == null)
            {
                desistenteStatus = new SalesApp.Models.ContractStatusEntity { Name = "Desistente" };
                context.ContractStatuses.Add(desistenteStatus);
                await context.SaveChangesAsync();
            }

            var testUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Test Comm User",
                Email = $"comm_user_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                RoleId = 3,
                IsActive = true
            };
            context.Users.Add(testUser);
            await context.SaveChangesAsync();

            context.UserClassifications.Add(new UserClassification
            {
                UserInternalId = testUser.InternalId,
                LevelId = alefLevel.Id,
                StartDate = DateTime.UtcNow.AddMonths(-12)
            });

            var now = DateTime.UtcNow;
            var refDate = CareerReferenceDate.Resolve(now, 15);

            // 1. Included: Active, age 4, R$ 100.000
            var cActive = new Contract { ContractNumber = $"COMM-ACT-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 100_000m, ContractStatusId = activeStatus.Id, SaleStartDate = refDate.AddMonths(-4), IsActive = true, HasPayment = true };
            // 2. Included: Late1, age 3, R$ 200.000
            var cLate1 = new Contract { ContractNumber = $"COMM-LT1-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 200_000m, ContractStatusId = late1Status.Id, SaleStartDate = refDate.AddMonths(-3), IsActive = true, HasPayment = true };
            // 3. Included: Late2, age 5, R$ 300.000
            var cLate2 = new Contract { ContractNumber = $"COMM-LT2-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 300_000m, ContractStatusId = late2Status.Id, SaleStartDate = refDate.AddMonths(-5), IsActive = true, HasPayment = true };
            // 4. Included: Late3, age 6, R$ 400.000
            var cLate3 = new Contract { ContractNumber = $"COMM-LT3-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 400_000m, ContractStatusId = late3Status.Id, SaleStartDate = refDate.AddMonths(-6), IsActive = true, HasPayment = true };
            // 5. Included: Defaulted, age 7, R$ 500.000
            var cDefaulted = new Contract { ContractNumber = $"COMM-DEF-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 500_000m, ContractStatusId = defaultedStatus.Id, SaleStartDate = refDate.AddMonths(-7), IsActive = true, HasPayment = true };

            // 6. Excluded: Active but age 1 (< 2), R$ 1.000.000
            var cTooYoung = new Contract { ContractNumber = $"COMM-YNG-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 1_000_000m, ContractStatusId = activeStatus.Id, SaleStartDate = refDate.AddMonths(-1), IsActive = true, HasPayment = true };
            // 7. Excluded: Desistente age 4, R$ 2.000.000
            var cDesistente = new Contract { ContractNumber = $"COMM-DES-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 2_000_000m, ContractStatusId = desistenteStatus.Id, SaleStartDate = refDate.AddMonths(-4), IsActive = false, HasPayment = false };
            // 8. Excluded: Older than 12m window (age 14), R$ 3.000.000
            var cTooOld = new Contract { ContractNumber = $"COMM-OLD-{Guid.NewGuid():N}"[..20], UserInternalId = testUser.InternalId, TotalAmount = 3_000_000m, ContractStatusId = activeStatus.Id, SaleStartDate = refDate.AddMonths(-14), IsActive = true, HasPayment = true };

            context.Contracts.AddRange(cActive, cLate1, cLate2, cLate3, cDefaulted, cTooYoung, cDesistente, cTooOld);
            await context.SaveChangesAsync();

            // Evaluate user
            var evalResponse = await clientWithToken.GetAsync($"/api/career-plan/users/{testUser.Id}");
            evalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var evalResult = await evalResponse.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            evalResult.Should().NotBeNull();
            evalResult!.Success.Should().BeTrue();

            // Expected sum: 100k + 200k + 300k + 400k + 500k = 1.500.000
            evalResult.Data!.RawMetrics.CommercializedContracts12mMinAge2.Should().Be(1_500_000m);

            // Compare with ActiveProduction12mMinPaid2: only cActive (100k) is Active with paid >= 2
            evalResult.Data.RawMetrics.ActiveProduction12mMinPaid2.Should().Be(100_000m);
        }

        [Fact]
        public async Task EvaluateUser_UnclassifiedUser_TargetsAlefWithCommercializedContractsRequirement()
        {
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.CreateClient();
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unclassifiedUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Unclassified Test User",
                Email = $"unclass_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                RoleId = 3,
                IsActive = true
            };
            context.Users.Add(unclassifiedUser);
            await context.SaveChangesAsync();

            // Evaluate via API
            var evalResponse = await clientWithToken.GetAsync($"/api/career-plan/users/{unclassifiedUser.Id}");
            evalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var evalResult = await evalResponse.Content.ReadFromJsonAsync<ApiResponse<CareerPlanEvaluationResponse>>();
            evalResult.Should().NotBeNull();
            evalResult!.Success.Should().BeTrue();
            evalResult.Data!.CurrentLevel.Should().BeNull();
            evalResult.Data.TargetLevel.Should().NotBeNull();
            evalResult.Data.TargetLevel!.Name.Should().Be("Alef");

            var commReq = evalResult.Data.Requirements.First(r => r.Metric == "CommercializedContracts");
            commReq.Should().NotBeNull();
            commReq.RawMetricKey.Should().Be("commercializedContracts12mMinAge2");
            commReq.Label.Should().Be("Contratos Comercializados ≥ Meta");
        }
    }
}
