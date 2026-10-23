using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SalesApp.DTOs;
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
            var response = await _client.GetAsync("/api/career-plan/config");
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
            var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>();
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

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
