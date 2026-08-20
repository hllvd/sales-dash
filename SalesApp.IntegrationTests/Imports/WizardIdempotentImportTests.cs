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
    [Collection("Imports Tests")]
    public class WizardIdempotentImportTests
    {
        private readonly HttpClient _client;
        private readonly ImportsTestFactory _factory;

        public WizardIdempotentImportTests(ImportsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task Step2Import_WhenCalledTwice_ShouldBeIdempotentAndSucceed()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = $"WIZARD-{Guid.NewGuid():N}";

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superadmin = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");

                var session = new ImportSession
                {
                    UploadId = uploadId,
                    FileName = "contracts.csv",
                    FileType = "csv",
                    UploadedByUserInternalId = superadmin.InternalId,
                    Status = "wizard_step1",
                    CreatedAt = DateTime.UtcNow
                };

                context.ImportSessions.Add(session);
                await context.SaveChangesAsync();
            }

            var csvUsersContent = @"Name,Email,ParentEmail,Matricula,Owner_Matricula,Password
Wizard User 1,wizard.user1@test.com,,MAT-WIZ-001,1,ChangeMe123!
Wizard User 2,wizard.user2@test.com,wizard.user1@test.com,MAT-WIZ-002,0,ChangeMe123!";

            // 1st Step2 Import
            var response1 = await PostStep2Import(uploadId, csvUsersContent, "users.csv");
            response1.StatusCode.Should().Be(HttpStatusCode.OK);

            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            result1!.Success.Should().BeTrue();
            result1.Data!.FailedRows.Should().Be(0);

            // 2nd Step2 Import (Re-importing the same file)
            var response2 = await PostStep2Import(uploadId, csvUsersContent, "users.csv");
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            result2!.Success.Should().BeTrue();
            result2.Data!.FailedRows.Should().Be(0);

            // Verify in DB that users exist and are active
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user1 = await context.Users.FirstOrDefaultAsync(u => u.Email == "wizard.user1@test.com");
                var user2 = await context.Users.FirstOrDefaultAsync(u => u.Email == "wizard.user2@test.com");

                user1.Should().NotBeNull();
                user1!.IsActive.Should().BeTrue();

                user2.Should().NotBeNull();
                user2!.IsActive.Should().BeTrue();
                user2.ParentUserId.Should().Be(user1.Id);
            }
        }

        private async Task<HttpResponseMessage> PostStep2Import(string uploadId, string csvContent, string fileName)
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(uploadId), "uploadId");

            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipart.Add(fileContent, "usersFile", fileName);

            return await _client.PostAsync("/api/wizard/step2-import", multipart);
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
