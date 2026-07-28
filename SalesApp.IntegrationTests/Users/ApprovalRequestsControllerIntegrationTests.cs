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

        [Fact]
        public async Task ChangeParentEmail_TargetingAdmin_OnlyTargetAdminCanApprove()
        {
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            // Create Admin A and Admin B
            var adminAEmail = $"admin_target_{Guid.NewGuid().ToString()[..6]}@test.com";
            var adminBEmail = $"admin_other_{Guid.NewGuid().ToString()[..6]}@test.com";
            var userEmail = $"user_req_{Guid.NewGuid().ToString()[..6]}@test.com";

            var regAdminA = await _client.PostAsJsonAsync("/api/users/admin-register", new { Name = "Admin Target", Email = adminAEmail, Password = "Password123!", TeamName = "Team A", ClassificationLevelId = 1, Role = "manager" });
            regAdminA.EnsureSuccessStatusCode();

            var regAdminB = await _client.PostAsJsonAsync("/api/users/admin-register", new { Name = "Admin Other", Email = adminBEmail, Password = "Password123!", TeamName = "Team B", ClassificationLevelId = 1, Role = "manager" });
            regAdminB.EnsureSuccessStatusCode();

            var meResponse = await _client.GetAsync("/api/users/me");
            meResponse.EnsureSuccessStatusCode();
            var meResult = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var superAdminId = meResult!.Data!.Id;

            var regUser = await _client.PostAsJsonAsync("/api/users/register", new { Name = "Requester User", Email = userEmail, Password = "Password123!", ParentUserId = superAdminId });
            regUser.EnsureSuccessStatusCode();

            // User creates ChangeParentEmail request targeting Admin A
            var userToken = await GetTokenAsync(userEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "ChangeParentEmail",
                PayloadJson = JsonSerializer.Serialize(new ChangeParentEmailPayload { NewParentEmail = adminAEmail })
            });
            createRes.EnsureSuccessStatusCode();
            var createData = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var requestId = createData!.Data!.Id;

            // Admin B (unrelated admin) should NOT see it in pending
            var adminBToken = await GetTokenAsync(adminBEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminBToken);
            var pendingResB = await _client.GetAsync("/api/approval-requests/pending");
            var pendingDataB = await pendingResB.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingDataB!.Data.Should().NotContain(r => r.Id == requestId);

            // Admin B trying to resolve should get 403 Forbidden
            var resolveResB = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve", new ResolveApprovalDto { Action = "Approved" });
            resolveResB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Admin A (target parent admin) CAN see and resolve it
            var adminAToken = await GetTokenAsync(adminAEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAToken);
            var pendingResA = await _client.GetAsync("/api/approval-requests/pending");
            var pendingDataA = await pendingResA.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingDataA!.Data.Should().Contain(r => r.Id == requestId);

            var resolveResA = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve", new ResolveApprovalDto { Action = "Approved" });
            resolveResA.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        [Fact]
        public async Task FullFlow_Admin1BecomesChildOfAdmin2_GetsOwnMatricula_JoinsAdmin2Matricula()
        {
            // ── Setup: authenticate as superadmin ──────────────────────────────────
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            var tag = Guid.NewGuid().ToString()[..6];
            var admin1Email = $"admin1_{tag}@test.com";
            var admin2Email = $"admin2_{tag}@test.com";
            var admin1MatriculaNumber = $"ADM1MAT{tag}".ToUpper();
            var admin2MatriculaNumber = $"ADM2MAT{tag}".ToUpper();

            // Register Admin1 and Admin2 as top-level admins
            var regAdmin1 = await _client.PostAsJsonAsync("/api/users/admin-register", new
            {
                Name = "Admin One",
                Email = admin1Email,
                Password = "Password123!",
                TeamName = $"Team1_{tag}",
                ClassificationLevelId = 1,
                Role = "manager"
            });
            regAdmin1.EnsureSuccessStatusCode();

            var regAdmin2 = await _client.PostAsJsonAsync("/api/users/admin-register", new
            {
                Name = "Admin Two",
                Email = admin2Email,
                Password = "Password123!",
                TeamName = $"Team2_{tag}",
                ClassificationLevelId = 1,
                Role = "manager"
            });
            regAdmin2.EnsureSuccessStatusCode();

            // ── Step 1: Admin1 asks to become a child of Admin2 ───────────────────
            var admin1Token = await GetTokenAsync(admin1Email, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);

            var changeParentRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "ChangeParentEmail",
                PayloadJson = JsonSerializer.Serialize(new ChangeParentEmailPayload { NewParentEmail = admin2Email })
            });
            changeParentRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var changeParentData = await changeParentRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var changeParentRequestId = changeParentData!.Data!.Id;
            changeParentData.Data.Status.Should().Be("Pending");

            // Admin2 can see the request in pending list
            var admin2Token = await GetTokenAsync(admin2Email, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin2Token);

            var pendingForAdmin2 = await _client.GetAsync("/api/approval-requests/pending");
            var pendingAdmin2Data = await pendingForAdmin2.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingAdmin2Data!.Data.Should().Contain(r => r.Id == changeParentRequestId);

            // Admin2 approves it
            var approveParentRes = await _client.PostAsJsonAsync($"/api/approval-requests/{changeParentRequestId}/resolve", new ResolveApprovalDto
            {
                Action = "Approved",
                Comment = "Welcome to my team"
            });
            approveParentRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var approveParentResult = await approveParentRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            approveParentResult!.Data!.Status.Should().Be("Approved");

            // Verify Admin1 is now a child of Admin2
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);
            var admin1MeRes = await _client.GetAsync("/api/users/me");
            var admin1MeData = await admin1MeRes.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            admin1MeData!.Data!.ParentEmail.Should().Be(admin2Email);

            // ── Step 2: Admin1 requests their own matricula ──────────────────────
            var ownMatriculaRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "AdminRequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = admin1MatriculaNumber })
            });
            ownMatriculaRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var ownMatriculaData = await ownMatriculaRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var ownMatriculaRequestId = ownMatriculaData!.Data!.Id;
            ownMatriculaData.Data.Status.Should().Be("Pending");

            // Superadmin can see it
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var pendingForSuperAdmin = await _client.GetAsync("/api/approval-requests/pending");
            var pendingSuper = await pendingForSuperAdmin.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingSuper!.Data.Should().Contain(r => r.Id == ownMatriculaRequestId);

            // Superadmin approves it
            var approveOwnMatRes = await _client.PostAsJsonAsync($"/api/approval-requests/{ownMatriculaRequestId}/resolve", new ResolveApprovalDto
            {
                Action = "Approved",
                Comment = "Matricula granted"
            });
            approveOwnMatRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Admin1 can now see the matricula in their own profile
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);
            var admin1AfterMatRes = await _client.GetAsync("/api/users/me");
            var admin1AfterMat = await admin1AfterMatRes.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            admin1AfterMat!.Data!.ActiveMatriculas.Should().Contain(m =>
                m.MatriculaNumber == admin1MatriculaNumber && m.IsOwner);

            // ── Step 3: Admin1 requests to be part of Admin2's matricula ─────────
            // First, Admin2 needs their own matricula so we can request it
            // Admin2 creates AdminRequestMatricula for admin2MatriculaNumber
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin2Token);
            var admin2MatReqRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "AdminRequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = admin2MatriculaNumber })
            });
            admin2MatReqRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var admin2MatReqData = await admin2MatReqRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var admin2MatReqId = admin2MatReqData!.Data!.Id;

            // Superadmin approves Admin2's matricula
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var approveAdmin2MatRes = await _client.PostAsJsonAsync($"/api/approval-requests/{admin2MatReqId}/resolve", new ResolveApprovalDto
            {
                Action = "Approved",
                Comment = "Admin2 matricula granted"
            });
            approveAdmin2MatRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Admin1 requests to join Admin2's matricula (non-owner link)
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);
            var joinMatRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "RequestMatricula",
                PayloadJson = JsonSerializer.Serialize(new RequestMatriculaPayload { MatriculaNumber = admin2MatriculaNumber })
            });
            joinMatRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var joinMatData = await joinMatRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var joinMatRequestId = joinMatData!.Data!.Id;
            joinMatData.Data.Status.Should().Be("Pending");

            // Admin2 (owner of that matricula) can see the request
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin2Token);
            var pendingForOwner = await _client.GetAsync("/api/approval-requests/pending");
            var pendingOwnerData = await pendingForOwner.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingOwnerData!.Data.Should().Contain(r => r.Id == joinMatRequestId);

            // Admin2 approves Admin1 joining their matricula
            var approveJoinRes = await _client.PostAsJsonAsync($"/api/approval-requests/{joinMatRequestId}/resolve", new ResolveApprovalDto
            {
                Action = "Approved",
                Comment = "Welcome to my matricula"
            });
            approveJoinRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Admin1 can now see Admin2's matricula in their active matriculas list
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);
            var admin1FinalRes = await _client.GetAsync("/api/users/me");
            var admin1Final = await admin1FinalRes.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();

            admin1Final!.Data!.ActiveMatriculas.Should().Contain(m =>
                m.MatriculaNumber == admin2MatriculaNumber && !m.IsOwner,
                "Admin1 should have non-owner access to Admin2's matricula");

            admin1Final.Data.ActiveMatriculas.Should().Contain(m =>
                m.MatriculaNumber == admin1MatriculaNumber && m.IsOwner,
                "Admin1 should still own their own matricula");
        }

        [Fact]
        public async Task ChangeParentEmail_TargetingSuperAdmin_AdminCannotSeeOrApproveOwnRequest()
        {
            // Setup: authenticate as superadmin and create an admin
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            var tag = Guid.NewGuid().ToString()[..6];
            var adminEmail = $"admin_sup_{tag}@test.com";

            var regAdmin = await _client.PostAsJsonAsync("/api/users/admin-register", new
            {
                Name = "Admin Requesting Super",
                Email = adminEmail,
                Password = "Password123!",
                TeamName = $"TeamSup_{tag}",
                ClassificationLevelId = 1,
                Role = "manager"
            });
            regAdmin.EnsureSuccessStatusCode();

            // Admin creates a ChangeParentEmail request targeting the superadmin
            var adminToken = await GetTokenAsync(adminEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "ChangeParentEmail",
                PayloadJson = JsonSerializer.Serialize(new ChangeParentEmailPayload { NewParentEmail = "superadmin@test.com" })
            });
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var createData = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var requestId = createData!.Data!.Id;
            createData.Data.Status.Should().Be("Pending");

            // The admin (requester) should NOT see the request in pending list
            var pendingRes = await _client.GetAsync("/api/approval-requests/pending");
            var pendingData = await pendingRes.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingData!.Data.Should().NotContain(r => r.Id == requestId,
                "Admin should not see a request targeting a superadmin in their own pending list");

            // The admin should get 403 if they try to resolve it directly
            var resolveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve",
                new ResolveApprovalDto { Action = "Approved" });
            resolveRes.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "Admin must not be able to approve a request targeting a superadmin");

            // Superadmin CAN see and approve it
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var pendingSuper = await _client.GetAsync("/api/approval-requests/pending");
            var pendingSuperData = await pendingSuper.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingSuperData!.Data.Should().Contain(r => r.Id == requestId,
                "Superadmin should see the request");

            var approveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve",
                new ResolveApprovalDto { Action = "Approved", Comment = "Approved by superadmin" });
            approveRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var approveData = await approveRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            approveData!.Data!.Status.Should().Be("Approved");
        }

        [Fact]
        public async Task RequestAdminRole_ParentAdminOrSuperAdminCanApprove_PromotesUserToAdmin()
        {
            var superAdminToken = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

            var tag = Guid.NewGuid().ToString()[..6];
            var parentAdminEmail = $"parentadmin_{tag}@test.com";
            var userEmail = $"userchild_{tag}@test.com";

            // Register parent admin
            var regParent = await _client.PostAsJsonAsync("/api/users/admin-register", new
            {
                Name = "Parent Admin",
                Email = parentAdminEmail,
                Password = "Password123!",
                TeamName = $"TeamRole_{tag}",
                ClassificationLevelId = 1,
                Role = "manager"
            });
            regParent.EnsureSuccessStatusCode();

            // Get Parent Admin's user ID
            var parentAdminToken = await GetTokenAsync(parentAdminEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentAdminToken);
            var parentMeRes = await _client.GetAsync("/api/users/me");
            var parentMeData = await parentMeRes.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var parentId = parentMeData!.Data!.Id;

            // Register user under parent admin
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            var regUser = await _client.PostAsJsonAsync("/api/users/register", new
            {
                Name = "User Requesting Admin",
                Email = userEmail,
                Password = "Password123!",
                ParentUserId = parentId
            });
            regUser.EnsureSuccessStatusCode();

            // User submits RequestAdminRole request
            var userToken = await GetTokenAsync(userEmail, "Password123!");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

            var createRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "RequestAdminRole",
                PayloadJson = "{}"
            });
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var createData = await createRes.Content.ReadFromJsonAsync<ApiResponse<ApprovalRequestResponse>>();
            var requestId = createData!.Data!.Id;

            // Parent Admin sees the request in pending list
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentAdminToken);
            var pendingRes = await _client.GetAsync("/api/approval-requests/pending");
            var pendingData = await pendingRes.Content.ReadFromJsonAsync<ApiResponse<List<ApprovalRequestResponse>>>();
            pendingData!.Data.Should().Contain(r => r.Id == requestId);

            // Parent Admin approves it
            var approveRes = await _client.PostAsJsonAsync($"/api/approval-requests/{requestId}/resolve",
                new ResolveApprovalDto { Action = "Approved", Comment = "Promoted to admin" });
            approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify user's role is now admin
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            var userMeRes = await _client.GetAsync("/api/users/me");
            var userMeData = await userMeRes.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            userMeData!.Data!.Role.ToLower().Should().Be("admin");

            // Subsequent RequestAdminRole request should fail because user is already admin
            var duplicateRes = await _client.PostAsJsonAsync("/api/approval-requests", new CreateApprovalRequestDto
            {
                RequestType = "RequestAdminRole",
                PayloadJson = "{}"
            });
            duplicateRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}

