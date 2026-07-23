using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Integration Tests")]
    public class ApprovalRequestsControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ApprovalRequestsControllerIntegrationTests(TestWebApplicationFactory factory)
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
            loginResponse.EnsureSuccessStatusCode();

            var json = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        }

        [Fact]
        public async Task CreateRequest_AndResolveWithApproval_ExecutesAction()
        {
            // 1. Authenticate superadmin
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");

            // 2. Register user under superadmin
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var meResponse = await _client.GetAsync("/api/users/me");
            meResponse.EnsureSuccessStatusCode();
            var meResult = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var superAdminId = meResult!.Data!.Id;

            var userEmail = $"req_user_{Guid.NewGuid().ToString()[..8]}@test.com";
            var registerRes = await _client.PostAsJsonAsync("/api/users/register", new
            {
                Name = "Requesting User",
                Email = userEmail,
                Password = "Password123!",
                ParentUserId = superAdminId
            });
            registerRes.EnsureSuccessStatusCode();

            // 3. Login as user and create request
            var userToken = await GetTokenAsync(userEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

            var createReq = new CreateApprovalRequestDto
            {
                RequestType = "RequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = $"MAT_{Guid.NewGuid().ToString()[..6]}" })
            };

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var createResult = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            createResult.Should().NotBeNull();
            createResult!.Success.Should().BeTrue();
            createResult.Data!.Status.Should().Be("Pending");
            var requestId = createResult.Data.Id;

            // 4. Check Pending list as SuperAdmin
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var pendingRes = await _client.GetAsync("/api/approval-requests/pending");
            pendingRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var pendingResult = await pendingRes.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingResult!.Data.Should().Contain(r => r.Id == requestId);

            // 5. Resolve as Approved
            var resolveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve", new ResolveApprovalDto
            {
                Action = "Approved",
                Comment = "Approved in test"
            });
            resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var resolveResult = await resolveRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            resolveResult!.Data!.Status.Should().Be("Approved");
            resolveResult.Data.ApproverComment.Should().Be("Approved in test");
        }

        [Fact]
        public async Task ResolveRequest_WithRejected_SetsStatusRejected()
        {
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            var createReq = new CreateApprovalRequestDto
            {
                RequestType = "RequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = $"REJ_{Guid.NewGuid().ToString()[..6]}" })
            };

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var createResult = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var requestId = createResult!.Data!.Id;

            var resolveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve", new ResolveApprovalDto
            {
                Action = "Rejected",
                Comment = "Not allowed"
            });
            resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var resolveResult = await resolveRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            resolveResult!.Data!.Status.Should().Be("Rejected");
            resolveResult.Data.ApproverComment.Should().Be("Not allowed");
        }

        [Fact]
        public async Task ResolveRequest_WithLater_KeepsStatusPending()
        {
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            var createReq = new CreateApprovalRequestDto
            {
                RequestType = "RequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = $"LAT_{Guid.NewGuid().ToString()[..6]}" })
            };

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var createResult = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var requestId = createResult!.Data!.Id;

            var resolveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve", new ResolveApprovalDto
            {
                Action = "Later",
                Comment = "Review next week"
            });
            resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var resolveResult = await resolveRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            resolveResult!.Data!.Status.Should().Be("Pending");
            resolveResult.Data.ApproverComment.Should().Be("Review next week");
        }
    }
}
