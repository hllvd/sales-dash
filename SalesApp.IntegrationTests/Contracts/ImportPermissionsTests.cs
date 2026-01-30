using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
using SalesApp.IntegrationTests;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    public class ImportPermissionsTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ImportPermissionsTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetTokenAsync(string email, string password)
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { email, password });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task SuperAdmin_ShouldSeeAllTemplates()
        {
            // Arrange
            var token = await GetTokenAsync("superadmin@test.com", "superadmin123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/imports/templates");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ImportTemplateResponse>>>();
            result!.Data.Should().Contain(t => t.Name == "Users");
            result.Data.Should().Contain(t => t.Name == "Contracts");
            result.Data.Should().Contain(t => t.Name == "contractDashboard");
        }

        [Fact]
        public async Task Admin_ShouldOnlySeeContractDashboardTemplate()
        {
            // Arrange
            var token = await GetTokenAsync("admin@test.com", "admin123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/imports/templates");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ImportTemplateResponse>>>();
            result!.Data.Should().HaveCount(1);
            result.Data.First().Name.Should().Be("contractDashboard");
        }

        [Fact]
        public async Task Admin_AttemptingOtherTemplate_ShouldReturnForbidden()
        {
            // Arrange
            var token = await GetTokenAsync("admin@test.com", "admin123");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act - Try uploading using Template 1 (Users)
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("test"));
            content.Add(fileContent, "file", "test.csv");
            
            var response = await _client.PostAsync("/api/imports/upload?templateId=1", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
