using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Classifications
{
    [Collection("Integration Tests")]
    public class ClassificationsControllerTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ClassificationsControllerTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task ManageLevelMembers_ShouldWorkCorrectly()
        {
            // 1. Authenticate as superadmin
            var token = await GetSuperAdminToken();
            var clientWithToken = _factory.Client;
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 2. Create a Classification Level
            var levelName = $"Int Test Level {Guid.NewGuid().ToString()[..8]}";
            var createLevelReq = new CreateClassificationLevelRequest
            {
                Name = levelName,
                Description = "Integration Test Level Description",
                Prize = "Bonus R$ 100",
                SalesGoal = 50000
            };

            var levelResponse = await clientWithToken.PostAsJsonAsync("/api/classifications/levels", createLevelReq);
            levelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var levelResult = await levelResponse.Content.ReadFromJsonAsync<ApiResponse<ClassificationLevelResponse>>();
            levelResult.Should().NotBeNull();
            levelResult!.Success.Should().BeTrue();
            levelResult.Data.Should().NotBeNull();
            
            var levelId = levelResult.Data!.Id;

            // 3. Retrieve members of the empty level
            var getMembersResponse = await clientWithToken.GetAsync($"/api/classifications/levels/{levelId}/members");
            getMembersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var membersResult = await getMembersResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserClassificationResponse>>>();
            membersResult.Should().NotBeNull();
            membersResult!.Success.Should().BeTrue();
            membersResult.Data.Should().BeEmpty();

            // 4. Assign superadmin to this level
            // Find current user profile to get superadmin's Guid ID
            var meResponse = await clientWithToken.GetAsync("/api/users/me");
            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var meResult = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            meResult.Should().NotBeNull();
            var adminUserId = meResult!.Data!.Id;

            var assignReq = new AssignUserLevelRequest
            {
                UserId = adminUserId,
                LevelId = levelId,
                StartDate = DateTime.UtcNow.Date
            };

            var assignResponse = await clientWithToken.PostAsJsonAsync("/api/classifications/assign", assignReq);
            assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var assignResult = await assignResponse.Content.ReadFromJsonAsync<ApiResponse<UserClassificationResponse>>();
            assignResult.Should().NotBeNull();
            assignResult!.Success.Should().BeTrue();
            
            var assignmentId = assignResult.Data!.Id;

            // 5. Retrieve level members again and verify superadmin is listed as active
            getMembersResponse = await clientWithToken.GetAsync($"/api/classifications/levels/{levelId}/members");
            getMembersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            membersResult = await getMembersResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserClassificationResponse>>>();
            membersResult.Should().NotBeNull();
            membersResult!.Success.Should().BeTrue();
            membersResult.Data.Should().HaveCount(1);
            membersResult.Data![0].Id.Should().Be(assignmentId);
            membersResult.Data[0].UserId.Should().Be(adminUserId);
            membersResult.Data[0].IsActive.Should().BeTrue();

            // 6. Delete/remove user from classification
            var removeResponse = await clientWithToken.DeleteAsync($"/api/classifications/assignments/{assignmentId}");
            removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Verify the user is now returned as inactive (not active)
            getMembersResponse = await clientWithToken.GetAsync($"/api/classifications/levels/{levelId}/members");
            getMembersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            membersResult = await getMembersResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserClassificationResponse>>>();
            membersResult.Should().NotBeNull();
            membersResult!.Success.Should().BeTrue();
            membersResult.Data.Should().HaveCount(1);
            membersResult.Data![0].Id.Should().Be(assignmentId);
            membersResult.Data[0].IsActive.Should().BeFalse();
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
