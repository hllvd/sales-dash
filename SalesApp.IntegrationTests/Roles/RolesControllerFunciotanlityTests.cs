
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
    }
}
