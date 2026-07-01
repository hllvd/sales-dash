using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SalesApp.DTOs;
using SalesApp.IntegrationTests;
using System.Collections.Generic;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Integration Tests")]
    public class MonitoringTests
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public MonitoringTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetTokenAsync(string email, string password)
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { email, password });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task SuperAdmin_ShouldGetMonitoringDataWithoutErrors()
        {
            // Arrange
            var token = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act 1: Get Contracts Health
            var contractsResponse = await _client.GetAsync("/api/monitoring/contracts");
            contractsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var contractsResult = await contractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<MatriculaHealthResponse>>>();
            contractsResult.Should().NotBeNull();
            contractsResult!.Success.Should().BeTrue();

            // Act 2: Get Equipes Health
            var equipesResponse = await _client.GetAsync("/api/monitoring/equipes");
            equipesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var equipesResult = await equipesResponse.Content.ReadFromJsonAsync<ApiResponse<List<TeamMatriculaHealthResponse>>>();
            equipesResult.Should().NotBeNull();
            equipesResult!.Success.Should().BeTrue();

            // Act 3: Get Admin Import Stats
            var adminsResponse = await _client.GetAsync("/api/monitoring/admins");
            adminsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var adminsResult = await adminsResponse.Content.ReadFromJsonAsync<ApiResponse<List<AdminImportStatsResponse>>>();
            adminsResult.Should().NotBeNull();
            adminsResult!.Success.Should().BeTrue();
        }
    }
}
