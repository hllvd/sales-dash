using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class UserImportWithEmailTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UserImportWithEmailTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task UserImport_WithSendEmailTrue_ShouldCreateUserAndAttemptEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.sendemail.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,SendEmail
John Doe,{uniqueEmail},true";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-with-email.csv");
            
            // Configure mappings
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "SendEmail", "SendEmail" }
                }
            };

            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Confirm import
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Success.Should().BeTrue();
            confirmResult.Data!.ProcessedRows.Should().Be(1);

            // Verify user was created
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
            user!.Name.Should().Be("John Doe");
        }

        [Fact]
        public async Task UserImport_WithSendEmailFalse_ShouldCreateUserWithoutEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.noemail.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,SendEmail
Jane Smith,{uniqueEmail},false";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-no-email.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "SendEmail", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithoutSendEmailColumn_ShouldCreateUserWithoutEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.default.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email
Bob Johnson,{uniqueEmail}";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-default.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithCaseInsensitiveSendEmail_ShouldWork()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.caseinsensitive.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,SENDEMAIL
Alice Wonder,{uniqueEmail},TRUE";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-case-insensitive.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "SENDEMAIL", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithFlexibleColumnName_ShouldAutoMap()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.flexible.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,send-email
Charlie Brown,{uniqueEmail},1";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-flexible.csv");
            
            // The auto-mapping should recognize "send-email" as "SendEmail"
            var previewResponse = await _client.GetAsync($"/api/imports/{uploadId}/status");
            previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "send-email", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithVariousBooleanValues_ShouldParseCorrectly()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var email1 = $"test.bool1.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email2 = $"test.bool2.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email3 = $"test.bool3.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email4 = $"test.bool4.{Guid.NewGuid().ToString()[..8]}@test.com";
            
            var csvContent = $@"Name,Email,SendEmail
User One,{email1},yes
User Two,{email2},sim
User Three,{email3},y
User Four,{email4},s";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-boolean-values.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "SendEmail", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Data!.ProcessedRows.Should().Be(4);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var user1 = await context.Users.FirstOrDefaultAsync(u => u.Email == email1);
            var user2 = await context.Users.FirstOrDefaultAsync(u => u.Email == email2);
            var user3 = await context.Users.FirstOrDefaultAsync(u => u.Email == email3);
            var user4 = await context.Users.FirstOrDefaultAsync(u => u.Email == email4);
            
            user1.Should().NotBeNull();
            user2.Should().NotBeNull();
            user3.Should().NotBeNull();
            user4.Should().NotBeNull();
        }

        private async Task<string> UploadUserFile(string content, string fileName)
        {
            var multipartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipartContent.Add(fileContent, "file", fileName);

            // Use template ID 1 for Users
            var response = await _client.PostAsync("/api/imports/upload?templateId=1", multipartContent);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!.UploadId;
        }

        private async Task<string> GetAdminToken()
        {
            // User imports require superadmin role
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
