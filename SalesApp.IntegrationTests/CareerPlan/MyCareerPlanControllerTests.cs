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
    public class MyCareerPlanControllerTests
    {
        private readonly MiscTestFactory _factory;

        public MyCareerPlanControllerTests(MiscTestFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetMyCareerPlan_WithoutAuth_ReturnsUnauthorized()
        {
            var unauthClient = _factory.CreateClient();
            unauthClient.DefaultRequestHeaders.Authorization = null;
            var response = await unauthClient.GetAsync("/api/career-plan/me");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetMyCareerPlan_AsUser_ReturnsPersonalCareerPlan()
        {
            var token = await GetToken("user@test.com", "user123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/career-plan/me");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MyCareerPlanResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Evaluation.Should().NotBeNull();
            result.Data.Evaluation.UserEmail.Should().Be("user@test.com");
        }

        [Fact]
        public async Task GetMyCareerPlan_SecondCall_ReturnsFromCache()
        {
            var token = await GetToken("user@test.com", "user123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // First call (populates cache)
            var response1 = await client.GetAsync("/api/career-plan/me");
            response1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Second call (served from cache)
            var response2 = await client.GetAsync("/api/career-plan/me");
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<MyCareerPlanResponse>>();
            result2.Should().NotBeNull();
            result2!.Data.Should().NotBeNull();
            result2.Data!.FromCache.Should().BeTrue();
        }

        [Fact]
        public async Task RefreshMyCareerPlan_AsUser_ForcesRecalculation()
        {
            var token = await GetToken("user@test.com", "user123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/api/career-plan/me/refresh", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MyCareerPlanResponse>>();
            result.Should().NotBeNull();
            result!.Data.Should().NotBeNull();
            result.Data!.FromCache.Should().BeFalse();
            result.Data.Evaluation.Should().NotBeNull();
        }

        [Fact]
        public async Task GetMyCareerPlan_AsAdmin_ReturnsPersonalCareerPlan()
        {
            var token = await GetToken("admin@test.com", "admin123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/career-plan/me");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<MyCareerPlanResponse>>();
            result.Should().NotBeNull();
            result!.Data.Should().NotBeNull();
            result.Data!.Evaluation.UserEmail.Should().Be("admin@test.com");
        }

        [Fact]
        public async Task GetMyContractsRt_AsUser_ReturnsOk()
        {
            var token = await GetToken("user@test.com", "user123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/career-plan/me/contracts-rt?page=1&pageSize=50");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractRtPageDto>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetMyContractsRt_InvalidPageSize_ReturnsBadRequest()
        {
            var token = await GetToken("user@test.com", "user123");
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/career-plan/me/contracts-rt?page=1&pageSize=999");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        private async Task<string> GetToken(string email, string password)
        {
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception($"Failed to get token for {email}");
        }
    }
}
