using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
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
        public async Task GetTemplates_AsAdmin_ShouldReturnHardcodedTemplates()
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
            result.Data.Should().HaveCount(2); // Users and Contracts templates
            result.Data.Should().Contain(t => t.Name == "Users" && t.EntityType == "User");
            result.Data.Should().Contain(t => t.Name == "Contracts" && t.EntityType == "Contract");
        }

        [Fact]
        public async Task GetTemplates_FilterByEntityType_ShouldReturnFilteredTemplates()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/imports/templates?entityType=User");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ImportTemplateResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(1);
            result.Data![0].Name.Should().Be("Users");
            result.Data[0].EntityType.Should().Be("User");
        }

        [Fact]
        public async Task GetTemplate_UsersTemplateById_ShouldReturnTemplate()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - Get Users template (ID 1)
            var response = await _client.GetAsync("/api/imports/templates/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(1);
            result.Data.Name.Should().Be("Users");
            result.Data.EntityType.Should().Be("User");
            result.Data.RequiredFields.Should().Contain("Name");
            result.Data.RequiredFields.Should().Contain("Email");
            result.Data.OptionalFields.Should().Contain("Surname");
            result.Data.OptionalFields.Should().Contain("Role");
            result.Data.OptionalFields.Should().Contain("ParentEmail");
        }

        [Fact]
        public async Task GetTemplate_ContractsTemplateById_ShouldReturnTemplate()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - Get Contracts template (ID 2)
            var response = await _client.GetAsync("/api/imports/templates/2");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportTemplateResponse>>();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(2);
            result.Data.Name.Should().Be("Contracts");
            result.Data.EntityType.Should().Be("Contract");
            result.Data.RequiredFields.Should().Contain("ContractNumber");
            result.Data.RequiredFields.Should().Contain("UserName");
            result.Data.RequiredFields.Should().Contain("UserSurname");
            result.Data.RequiredFields.Should().Contain("TotalAmount");
            result.Data.RequiredFields.Should().Contain("GroupId");
        }

        [Fact]
        public async Task GetTemplate_InvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - Try to get non-existent template
            var response = await _client.GetAsync("/api/imports/templates/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
