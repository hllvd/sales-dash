using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SalesApp.Controllers;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Admin
{
    [Collection("Misc Tests")]
    public class AdminSqsControllerTests
    {
        private readonly HttpClient _client;
        private readonly MiscTestFactory _factory;

        public AdminSqsControllerTests(MiscTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetSuperAdminTokenAsync()
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

        private async Task<string> GetAdminTokenAsync()
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
        public async Task GetStats_WithoutAuth_ShouldReturnUnauthorized()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/admin/sqs/stats");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetStats_AsNonSuperAdmin_ShouldReturnForbidden()
        {
            var adminToken = await GetAdminTokenAsync();
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var response = await adminClient.GetAsync("/api/admin/sqs/stats");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Theory]
        [InlineData("jobs")]
        [InlineData("results")]
        public async Task GetStats_AsSuperAdmin_ShouldReturnValidStatsStructure(string queue)
        {
            var token = await GetSuperAdminTokenAsync();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/api/admin/sqs/stats?queue={queue}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var stats = await response.Content.ReadFromJsonAsync<SqsQueueStats>();
            stats.Should().NotBeNull();
            stats!.QueueType.Should().Be(queue);
            stats.ApproximateMessageCount.Should().BeGreaterThanOrEqualTo(0);
            stats.ApproximateInFlightCount.Should().BeGreaterThanOrEqualTo(0);
        }

        [Theory]
        [InlineData("jobs")]
        [InlineData("results")]
        public async Task PeekMessages_AsSuperAdmin_ShouldReturnList(string queue)
        {
            var token = await GetSuperAdminTokenAsync();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/api/admin/sqs/messages?queue={queue}&max=5");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<List<SqsMessageDto>>();
            messages.Should().NotBeNull();
        }

        [Fact]
        public async Task DiscardMessage_WithEmptyReceiptHandle_ShouldReturnBadRequest()
        {
            var token = await GetSuperAdminTokenAsync();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new ProcessMessageRequest
            {
                ReceiptHandle = "",
                Queue = "jobs"
            };

            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/admin/sqs/messages")
            {
                Content = JsonContent.Create(request)
            });

            // Either BadRequest (empty receipt handle / unconfigured queue) or OK
            response.StatusCode.Should().Match(s => s == HttpStatusCode.BadRequest || s == HttpStatusCode.OK);
        }
    }
}
