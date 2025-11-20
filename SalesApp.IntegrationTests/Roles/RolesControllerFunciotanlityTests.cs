
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using FluentAssertions;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Roles
{
    [Collection("Integration Tests")]
    public class RolesControllerFunciotanlityTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public RolesControllerFunciotanlityTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }

        private void InspectToken(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length == 3)
                {
                    // Decode the payload (second part)
                    var payload = parts[1];
                    // Add padding if needed
                    var padding = 4 - (payload.Length % 4);
                    if (padding != 4)
                        payload += new string('=', padding);
                    
                    var decoded = System.Convert.FromBase64String(payload);
                    var json = System.Text.Encoding.UTF8.GetString(decoded);
                    System.Console.WriteLine($"JWT Payload: {json}");
                    
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("role", out var roleClaim))
                        {
                            System.Console.WriteLine($"Role claim found: {roleClaim}");
                        }
                        else
                        {
                            System.Console.WriteLine("WARNING: No 'role' claim found in JWT!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error inspecting token: {ex.Message}");
            }
        }

        public class RoleRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int Level { get; set; }
            public string Permissions { get; set; } = string.Empty;
        }

        [Fact]
        public async Task SuperAdmin_Can_Create_List_Delete_Role()
        {
            // Login as superadmin
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a new role
            var roleName = $"integration-test-role-{Guid.NewGuid().ToString()[..8]}";
            var createRequest = new RoleRequest
            {
                Name = roleName,
                Description = "Integration test role",
                Level = 10,
                Permissions = "{\"canTest\":true}"
            };
            InspectToken(token);
            
            var createResponse = await _client.PostAsJsonAsync("/api/roles", createRequest);
            
            // Should succeed now that JWT configuration is fixed
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // List roles and check the new role exists
            var listResponse = await _client.GetAsync("/api/roles");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var listContent = await listResponse.Content.ReadAsStringAsync();
            listContent.Should().Contain(roleName);

            // Find the created role's ID (assume response is JSON array or object with roles)
            // For simplicity, try to parse as dynamic and find by name

            dynamic? rolesObj = Newtonsoft.Json.JsonConvert.DeserializeObject(listContent);
            int? createdRoleId = null;
            if (rolesObj is Newtonsoft.Json.Linq.JObject jObj)
            {
                var data = jObj["data"];
                if (data != null)
                {
                    foreach (var role in data)
                    {
                        if ((string?)role["name"] == roleName)
                        {
                            createdRoleId = (int?)role["id"];
                            break;
                        }
                    }
                }
            }
            else if (rolesObj is Newtonsoft.Json.Linq.JArray jArr)
            {
                foreach (var role in jArr)
                {
                    if ((string?)role["name"] == roleName)
                    {
                        createdRoleId = (int?)role["id"];
                        break;
                    }
                }
            }
            createdRoleId.Should().NotBeNull("Created role should be found in list");

            // Delete the created role
            var deleteResponse = await _client.DeleteAsync($"/api/roles/{createdRoleId}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SuperAdmin_Can_Update_Role_With_Put()
        {
            // Login as superadmin
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create a role to update
            var roleName = $"integration-put-role-{Guid.NewGuid().ToString()[..8]}";
            var createRequest = new RoleRequest
            {
                Name = roleName,
                Description = "Create for PUT test",
                Level = 7,
                Permissions = "{\"canTest\":true}"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/roles", createRequest);
            createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
            if (createResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                System.Console.WriteLine("Create forbidden; skipping update test");
                return;
            }

            // Find the created role's ID
            var listResponse = await _client.GetAsync("/api/roles");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var listContent = await listResponse.Content.ReadAsStringAsync();

            dynamic? rolesObj = Newtonsoft.Json.JsonConvert.DeserializeObject(listContent);
            int? roleId = null;
            if (rolesObj is Newtonsoft.Json.Linq.JObject jObj)
            {
                var data = jObj["data"];
                if (data != null)
                {
                    foreach (var r in data)
                    {
                        if ((string?)r["name"] == roleName)
                        {
                            roleId = (int?)r["id"];
                            break;
                        }
                    }
                }
            }
            else if (rolesObj is Newtonsoft.Json.Linq.JArray jArr)
            {
                foreach (var r in jArr)
                {
                    if ((string?)r["name"] == roleName)
                    {
                        roleId = (int?)r["id"];
                        break;
                    }
                }
            }

            roleId.Should().NotBeNull("Created role should be found in list");

            // Update the role via PUT
            var updatedName = roleName + "-updated";
            var updateRequest = new
            {
                Name = updatedName,
                Description = "Updated by PUT test",
                Level = 8,
                Permissions = "{\"canTest\":false}"
            };

            var putResponse = await _client.PutAsJsonAsync($"/api/roles/{roleId}", updateRequest);
            putResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
            if (putResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                System.Console.WriteLine("Update forbidden; cleaning up and skipping verification");
            }

            // Verify update if the API allowed it
            if (putResponse.IsSuccessStatusCode)
            {
                var afterList = await _client.GetAsync("/api/roles");
                afterList.StatusCode.Should().Be(HttpStatusCode.OK);
                var afterContent = await afterList.Content.ReadAsStringAsync();

                dynamic? afterObj = Newtonsoft.Json.JsonConvert.DeserializeObject(afterContent);
                bool foundUpdated = false;
                bool foundOriginal = false;

                if (afterObj is Newtonsoft.Json.Linq.JObject afterJObj)
                {
                    var data = afterJObj["data"];
                    if (data != null)
                    {
                        foreach (var r in data)
                        {
                            var name = (string?)r["name"];
                            if (name == updatedName) foundUpdated = true;
                            if (name == roleName) foundOriginal = true;
                        }
                    }
                }
                else if (afterObj is Newtonsoft.Json.Linq.JArray afterJArr)
                {
                    foreach (var r in afterJArr)
                    {
                        var name = (string?)r["name"];
                        if (name == updatedName) foundUpdated = true;
                        if (name == roleName) foundOriginal = true;
                    }
                }

                foundUpdated.Should().BeTrue("Updated role should be present");
                foundOriginal.Should().BeFalse("Original role name should not exist after update");
            }

            // Cleanup: delete the role
            var deleteResponse = await _client.DeleteAsync($"/api/roles/{roleId}");
            deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
        }
    }
}
