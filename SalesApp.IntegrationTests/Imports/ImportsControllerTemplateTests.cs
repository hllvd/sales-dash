using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class ImportsControllerTemplateTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ImportsControllerTemplateTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task CreateTemplate_AsSuperAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                Name = $"Test Template {Guid.NewGuid().ToString()[..8]}",
                EntityType = "Contract",
                Description = "Test template for contracts",
                RequiredFields = new[] { "ContractNumber", "UserName", "UserSurname", "TotalAmount", "GroupId" },
                OptionalFields = new[] { "Status", "SaleStartDate" },
                DefaultMappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "Total", "TotalAmount" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/imports/templates", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Name.Should().Be(request.Name);
            result.Data.EntityType.Should().Be("Contract");
        }

        [Fact]
        public async Task CreateTemplate_AsAdmin_ShouldReturnForbidden()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                Name = "Test Template",
                EntityType = "Contract",
                Description = "Test",
                RequiredFields = new[] { "ContractNumber" },
                OptionalFields = new string[] { },
                DefaultMappings = new Dictionary<string, string>()
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/imports/templates", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetTemplates_AsAdmin_ShouldReturnList()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/imports/templates");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ImportTemplateResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetTemplate_ById_ShouldReturnTemplate()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a template first
            var createRequest = new
            {
                Name = $"Get Test {Guid.NewGuid().ToString()[..8]}",
                EntityType = "Contract",
                Description = "Test",
                RequiredFields = new[] { "ContractNumber" },
                OptionalFields = new string[] { },
                DefaultMappings = new Dictionary<string, string>()
            };

            var createResponse = await _client.PostAsJsonAsync("/api/imports/templates", createRequest);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            var templateId = createResult!.Data!.Id;

            // Act
            var response = await _client.GetAsync($"/api/imports/templates/{templateId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Id.Should().Be(templateId);
        }

        [Fact]
        public async Task UpdateTemplate_AsSuperAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a template first
            var createRequest = new
            {
                Name = $"Update Test {Guid.NewGuid().ToString()[..8]}",
                EntityType = "Contract",
                Description = "Original",
                RequiredFields = new[] { "ContractNumber" },
                OptionalFields = new string[] { },
                DefaultMappings = new Dictionary<string, string>()
            };

            var createResponse = await _client.PostAsJsonAsync("/api/imports/templates", createRequest);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            var templateId = createResult!.Data!.Id;

            // Update request
            var updateRequest = new
            {
                Name = createRequest.Name, // Keep same name
                EntityType = "Contract",
                Description = "Updated Description",
                RequiredFields = new[] { "ContractNumber", "TotalAmount" },
                OptionalFields = new[] { "Status" },
                DefaultMappings = new Dictionary<string, string> { { "Total", "TotalAmount" } }
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/imports/templates/{templateId}", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            result!.Success.Should().BeTrue();
            result.Data!.Description.Should().Be("Updated Description");
            result.Data.RequiredFields.Should().Contain("TotalAmount");
        }

        [Fact]
        public async Task DeleteTemplate_AsSuperAdmin_ShouldSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create a template first
            var createRequest = new
            {
                Name = $"Delete Test {Guid.NewGuid().ToString()[..8]}",
                EntityType = "Contract",
                Description = "To be deleted",
                RequiredFields = new[] { "ContractNumber" },
                OptionalFields = new string[] { },
                DefaultMappings = new Dictionary<string, string>()
            };

            var createResponse = await _client.PostAsJsonAsync("/api/imports/templates", createRequest);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            var templateId = createResult!.Data!.Id;

            // Act
            var response = await _client.DeleteAsync($"/api/imports/templates/{templateId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify it's deleted
            var getResponse = await _client.GetAsync($"/api/imports/templates/{templateId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        private async Task<string> GetAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "admin@test.com",
                Password = "admin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
