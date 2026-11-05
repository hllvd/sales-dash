using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Groups
{
    [Collection("Misc Tests")]
    public class GroupsControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly MiscTestFactory _factory;

        public GroupsControllerIntegrationTests(MiscTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<HttpClient> GetAuthenticatedClient(string email, string password)
        {
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var token = result?.Data?.Token ?? throw new Exception($"Failed to authenticate {email}");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task CreateGroup_WithSegment_ShouldPersistAndReturnSegment()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            var groupName = $"Group-{Guid.NewGuid().ToString()[..8]}";
            var request = new GroupRequest
            {
                Name = groupName,
                Description = "Test group with segment",
                Commission = 15.5m,
                Segment = 1 // Lar
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/groups", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GroupResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Name.Should().Be(groupName);
            result.Data.Segment.Should().Be(1);
            result.Data.SegmentName.Should().Be("Lar");

            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Groups.FindAsync(result.Data.Id);
            saved.Should().NotBeNull();
            saved!.Segment.Should().Be(1);
        }

        [Fact]
        public async Task UpdateGroup_ShouldUpdateSegmentAndAllowReactivation()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            int groupId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var group = new Group
                {
                    Name = $"UpdGrp-{Guid.NewGuid().ToString()[..8]}",
                    Description = "Initial",
                    Commission = 5m,
                    Segment = 1,
                    IsActive = true
                };
                db.Groups.Add(group);
                await db.SaveChangesAsync();
                groupId = group.Id;
            }

            // Deactivate
            var delResponse = await client.DeleteAsync($"/api/groups/{groupId}");
            delResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Update & Reactivate with new segment 3 (Serviços)
            var updateRequest = new UpdateGroupRequest
            {
                Segment = 3,
                IsActive = true,
                Description = "Reactivated with new segment"
            };

            var updateResponse = await client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<GroupResponse>>();
            result!.Data.Segment.Should().Be(3);
            result.Data.SegmentName.Should().Be("Serviços");
            result.Data.IsActive.Should().BeTrue();
        }
    }
}
