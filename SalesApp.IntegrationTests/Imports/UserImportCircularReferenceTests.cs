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
    public class UserImportCircularReferenceTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UserImportCircularReferenceTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task UserImport_IntraBatchCycle_ShouldBreakCycleAndAddWarning()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // A -> B -> C -> A
            var emailA = $"cycle.a.{Guid.NewGuid().ToString()[..4]}@test.com";
            var emailB = $"cycle.b.{Guid.NewGuid().ToString()[..4]}@test.com";
            var emailC = $"cycle.c.{Guid.NewGuid().ToString()[..4]}@test.com";

            var csvContent = $@"Name,Email,ParentEmail,Matricula
User A,{emailA},{emailB},MAT-A
User B,{emailB},{emailC},MAT-B
User C,{emailC},{emailA},MAT-C";

            // Act
            var uploadId = await UploadUserFile(csvContent, "intra-batch-cycle.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "ParentEmail", "ParentEmail" },
                    { "Matricula", "Matricula" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Success.Should().BeTrue();
            
            // Should contain a warning about circular references
            confirmResult.Data!.Warnings.Should().Contain(w => w.Contains("referências circulares") && w.Contains(emailA));

            // Verify users were created but cycle was broken (at least one should have null parent)
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var userA = await context.Users.FirstOrDefaultAsync(u => u.Email == emailA);
            var userB = await context.Users.FirstOrDefaultAsync(u => u.Email == emailB);
            var userC = await context.Users.FirstOrDefaultAsync(u => u.Email == emailC);

            userA.Should().NotBeNull();
            userB.Should().NotBeNull();
            userC.Should().NotBeNull();

            // In a cycle A->B->C->A, at least one parent must be null to break the cycle
            bool anyNullParent = userA!.ParentUserId == null || userB!.ParentUserId == null || userC!.ParentUserId == null;
            anyNullParent.Should().BeTrue();
        }

        [Fact]
        public async Task UserImport_CrossBoundaryCycle_ShouldBlockUpdateAndAddWarning()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Create User A and User B in DB
            // User A is parent of User B
            var emailA = $"existing.a.{Guid.NewGuid().ToString()[..4]}@test.com";
            var emailB = $"existing.b.{Guid.NewGuid().ToString()[..4]}@test.com";

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var userA = new User { Name = "Existing A", Email = emailA, PasswordHash = "hash", RoleId = 3 };
                context.Users.Add(userA);
                await context.SaveChangesAsync();

                var userB = new User { Name = "Existing B", Email = emailB, PasswordHash = "hash", RoleId = 3, ParentUserId = userA.Id };
                context.Users.Add(userB);
                await context.SaveChangesAsync();
            }

            // 2. Import User A with User B as parent (creating a cycle: A -> B -> A)
            var csvContent = $@"Name,Email,ParentEmail,Matricula
Existing A,{emailA},{emailB},MAT-A-UP";

            // Act
            var uploadId = await UploadUserFile(csvContent, "cross-boundary-cycle.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "ParentEmail", "ParentEmail" },
                    { "Matricula", "Matricula" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            
            confirmResult!.Data!.Warnings.Should().Contain(w => w.Contains("referências circulares") && w.Contains(emailA));

            // Verify User A's parent is still null (or not User B)
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var userA = await context.Users.FirstOrDefaultAsync(u => u.Email == emailA);
                var userB = await context.Users.FirstOrDefaultAsync(u => u.Email == emailB);
                
                userA!.ParentUserId.Should().NotBe(userB!.Id);
            }
        }

        private async Task<string> UploadUserFile(string content, string fileName)
        {
            var multipartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipartContent.Add(fileContent, "file", fileName);

            var response = await _client.PostAsync("/api/imports/upload?templateId=1", multipartContent);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!.UploadId;
        }

        private async Task<string> GetAdminToken()
        {
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
