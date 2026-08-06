using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Users Tests")]
    public class UserMetadataControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly UsersTestFactory _factory;

        public UserMetadataControllerIntegrationTests(UsersTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        // ==========================================
        // FIELD MANAGEMENT TESTS
        // ==========================================

        [Fact]
        public async Task SuperAdmin_CanManageMetadataFields()
        {
            var token = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 1. Create Field
            var fieldRequest = new UserMetadataFieldRequest(
                Key: $"field_{Guid.NewGuid().ToString()[..8]}",
                Label: "Test Field Label",
                GroupLabel: "Test Group",
                FieldType: "text",
                DropdownOptions: null,
                DisplayOrder: 10,
                IsRequired: true
            );

            var createResponse = await client.PostAsJsonAsync("/api/usermetadata/fields", fieldRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserMetadataFieldResponse>>();
            createResult.Should().NotBeNull();
            createResult!.Success.Should().BeTrue();
            createResult.Data!.Key.Should().Be(fieldRequest.Key);
            createResult.Data.Label.Should().Be(fieldRequest.Label);
            createResult.Data.IsRequired.Should().BeTrue();

            var fieldId = createResult.Data.Id;

            // 2. Update Field
            var updateRequest = new UserMetadataFieldRequest(
                Key: fieldRequest.Key,
                Label: "Updated Field Label",
                GroupLabel: "Updated Group",
                FieldType: "dropdown",
                DropdownOptions: "[\"A\", \"B\"]",
                DisplayOrder: 20,
                IsRequired: false
            );

            var updateResponse = await client.PutAsJsonAsync($"/api/usermetadata/fields/{fieldId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var updateResult = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserMetadataFieldResponse>>();
            updateResult.Should().NotBeNull();
            updateResult!.Data!.Label.Should().Be("Updated Field Label");
            updateResult.Data.FieldType.Should().Be("dropdown");
            updateResult.Data.IsRequired.Should().BeFalse();

            // 3. Delete Field
            var deleteResponse = await client.DeleteAsync($"/api/usermetadata/fields/{fieldId}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get fields list and verify it is marked inactive (soft deleted)
            var getResponse = await client.GetAsync("/api/usermetadata/fields");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserMetadataFieldResponse>>>();
            getResult!.Data.Should().Contain(f => f.Id == fieldId && !f.IsActive);
        }

        [Fact]
        public async Task NonSuperAdmin_CannotManageMetadataFields()
        {
            var adminToken = await GetAdminToken();
            var userToken = await GetUserToken();

            var fieldRequest = new UserMetadataFieldRequest(
                Key: "unauthorized_field",
                Label: "Unauthorized Label",
                GroupLabel: null,
                FieldType: "text",
                DropdownOptions: null,
                DisplayOrder: 0,
                IsRequired: false
            );

            // Admin trying to create
            var clientAdmin = _factory.Client;
            clientAdmin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
            var adminResponse = await clientAdmin.PostAsJsonAsync("/api/usermetadata/fields", fieldRequest);
            adminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // User trying to create
            var clientUser = _factory.Client;
            clientUser.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);
            var userResponse = await clientUser.PostAsJsonAsync("/api/usermetadata/fields", fieldRequest);
            userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ==========================================
        // VALUE ACCESS AND UPDATE TESTS
        // ==========================================

        [Fact]
        public async Task User_CanGetAndUpdateOwnMetadataValues()
        {
            // Seed a field in the database
            var fieldId = await CreateActiveFieldInDb("user_own_test", "Own Test Field", false);

            var userToken = await GetUserToken();
            var userId = await GetUserIdByEmail("user@test.com");

            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            // Get values
            var getResponse = await client.GetAsync($"/api/usermetadata/{userId}/values");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserMetadataGroupDto>>>();
            getResult!.Success.Should().BeTrue();

            // Update value
            var upsertRequest = new UpsertUserMetadataRequest(new List<UserMetadataValueItem>
            {
                new UserMetadataValueItem(fieldId, "My Custom Value")
            });

            var updateResponse = await client.PutAsJsonAsync($"/api/usermetadata/{userId}/values", upsertRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify updated value
            var verifyResponse = await client.GetAsync($"/api/usermetadata/{userId}/values");
            var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserMetadataGroupDto>>>();
            
            var flatFields = new List<UserMetadataFieldValueDto>();
            verifyResult!.Data!.ForEach(g => flatFields.AddRange(g.Fields));
            
            var matchedField = flatFields.Find(f => f.FieldId == fieldId);
            matchedField.Should().NotBeNull();
            matchedField!.Value.Should().Be("My Custom Value");
        }

        [Fact]
        public async Task Admin_CanUpdateChildUserMetadata_ButNotNonDescendant()
        {
            // Create a child user under Admin (admin@test.com)
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminUser = await context.Users.FirstAsync(u => u.Email == "admin@test.com");
            var superAdminUser = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

            var childUser = new User
            {
                Name = "Admin Child User",
                Email = $"child_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3,
                ParentUserId = adminUser.Id,
                IsActive = true
            };
            
            var otherUser = new User
            {
                Name = "Other User under Superadmin",
                Email = $"other_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                RoleId = 3,
                ParentUserId = superAdminUser.Id, // Not a child of admin
                IsActive = true
            };

            context.Users.Add(childUser);
            context.Users.Add(otherUser);
            await context.SaveChangesAsync();

            var fieldId = await CreateActiveFieldInDb("admin_child_test", "Child Test Field", false);
            var adminToken = await GetAdminToken();

            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // 1. Admin should succeed in updating child
            var upsertChildRequest = new UpsertUserMetadataRequest(new List<UserMetadataValueItem>
            {
                new UserMetadataValueItem(fieldId, "Value Set By Admin")
            });
            var childResponse = await client.PutAsJsonAsync($"/api/usermetadata/{childUser.Id}/values", upsertChildRequest);
            childResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Admin should fail (403 Forbidden) in updating non-descendant otherUser
            var otherResponse = await client.PutAsJsonAsync($"/api/usermetadata/{otherUser.Id}/values", upsertChildRequest);
            otherResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task User_CannotUpdateOtherUserMetadata()
        {
            var userToken = await GetUserToken();
            var targetUserId = await GetUserIdByEmail("admin@test.com"); // target is admin
            var fieldId = await CreateActiveFieldInDb("user_other_test", "Other Test Field", false);

            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            var upsertRequest = new UpsertUserMetadataRequest(new List<UserMetadataValueItem>
            {
                new UserMetadataValueItem(fieldId, "Malicious Value")
            });

            var response = await client.PutAsJsonAsync($"/api/usermetadata/{targetUserId}/values", upsertRequest);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task RequiredField_ReturnsBadRequest_WhenEmpty()
        {
            var fieldId = await CreateActiveFieldInDb("required_test_field", "Required Field", true); // IsRequired = true
            var userToken = await GetUserToken();
            var userId = await GetUserIdByEmail("user@test.com");

            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            // Try to upsert empty value
            var upsertRequest = new UpsertUserMetadataRequest(new List<UserMetadataValueItem>
            {
                new UserMetadataValueItem(fieldId, "")
            });

            var response = await client.PutAsJsonAsync($"/api/usermetadata/{userId}/values", upsertRequest);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("obrigatório");
        }

        // ==========================================
        // HELPERS
        // ==========================================

        private async Task<int> CreateActiveFieldInDb(string key, string label, bool isRequired)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var field = await context.UserMetadataFields.FirstOrDefaultAsync(f => f.Key == key);
            if (field == null)
            {
                field = new UserMetadataField
                {
                    Key = key,
                    Label = label,
                    FieldType = "text",
                    IsRequired = isRequired,
                    IsActive = true
                };
                context.UserMetadataFields.Add(field);
                await context.SaveChangesAsync();
            }
            else if (field.IsRequired != isRequired || !field.IsActive)
            {
                field.IsRequired = isRequired;
                field.IsActive = true;
                context.UserMetadataFields.Update(field);
                await context.SaveChangesAsync();
            }
            return field.Id;
        }

        private async Task<Guid> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstAsync(u => u.Email == email);
            return user.Id;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest { Email = "superadmin@test.com", Password = "superadmin123" };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest { Email = "admin@test.com", Password = "admin123" };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest { Email = "user@test.com", Password = "user123" };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token");
        }
    }
}
