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
    public class ImportUpsertTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ImportUpsertTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task ImportContract_WhenDuplicate_ShouldUpdateStatus()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"UPSERT-{Guid.NewGuid().ToString()[..8]}";
            
            // 1. Create initial contract
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await context.Users.FirstAsync();
                var group = await context.Groups.FirstAsync();

                var contract = new Contract
                {
                    ContractNumber = contractNumber,
                    UserId = user.Id,
                    TotalAmount = 1000,
                    GroupId = group.Id,
                    Status = "active",
                    IsActive = true
                };
                context.Contracts.Add(contract);
                await context.SaveChangesAsync();
            }

            // 2. Import same contract number with different status
            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status
{contractNumber},superadmin@test.com,5000,0,late1";

            var uploadId = await UploadFile(csvContent, "upsert.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            
            // Confirm import
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Success.Should().BeTrue();
            confirmResult.Data!.ProcessedRows.Should().Be(1);
            confirmResult.Data.FailedRows.Should().Be(0);

            // Verify contract was updated
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                
                contract.Should().NotBeNull();
                contract!.Status.Should().Be("late1");
                contract.TotalAmount.Should().Be(5000);
            }
        }

        private async Task<string> UploadFile(string content, string fileName)
        {
            var multipartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipartContent.Add(fileContent, "file", fileName);

            var response = await _client.PostAsync("/api/imports/upload?templateId=2", multipartContent);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!.UploadId;
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
