using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Groups
{
    [Collection("Misc Tests")]
    public class GroupsControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly MiscTestFactory _factory;

        public GroupsControllerIntegrationTests(MiscTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<HttpClient> GetAuthenticatedClient(string email, string password)
        {
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var token = result?.Data?.Token ?? throw new Exception($"Failed to authenticate {email}");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task CreateGroup_WithSegment_ShouldPersistAndReturnSegment()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            var groupName = $"Group-{Guid.NewGuid().ToString()[..8]}";
            var request = new GroupRequest
            {
                Name = groupName,
                Description = "Test group with segment",
                Commission = 15.5m,
                Segment = 1 // Lar
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/groups", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GroupResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Name.Should().Be(groupName);
            result.Data.Segment.Should().Be(1);
            result.Data.SegmentName.Should().Be("Lar");

            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Groups.FindAsync(result.Data.Id);
            saved.Should().NotBeNull();
            saved!.Segment.Should().Be(1);
        }

        [Fact]
        public async Task UpdateGroup_ShouldUpdateSegmentAndAllowReactivation()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            int groupId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var group = new Group
                {
                    Name = $"UpdGrp-{Guid.NewGuid().ToString()[..8]}",
                    Description = "Initial",
                    Commission = 5m,
                    Segment = 1,
                    IsActive = true
                };
                db.Groups.Add(group);
                await db.SaveChangesAsync();
                groupId = group.Id;
            }

            // Deactivate
            var delResponse = await client.DeleteAsync($"/api/groups/{groupId}");
            delResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Update & Reactivate with new segment 3 (Serviços)
            var updateRequest = new UpdateGroupRequest
            {
                Segment = 3,
                IsActive = true,
                Description = "Reactivated with new segment"
            };

            var updateResponse = await client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<GroupResponse>>();
            result!.Data.Segment.Should().Be(3);
            result.Data.SegmentName.Should().Be("Serviços");
            result.Data.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task DownloadImportTemplate_ShouldReturnExcelFile()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            // Act
            var response = await client.GetAsync("/api/groups/import/template");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ImportGroups_ShouldUpsertGroupsCorrectly()
        {
            // Arrange
            var client = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            var existingName = $"ExistGrp-{Guid.NewGuid().ToString()[..6]}";
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Groups.Add(new Group
                {
                    Name = existingName,
                    Description = "Original Desc",
                    Commission = 20,
                    Segment = 1, // Lar
                    IsActive = false // was inactive
                });
                await db.SaveChangesAsync();
            }

            var newGroupName = $"NewGrp-{Guid.NewGuid().ToString()[..6]}";
            var nullSegGroupName = $"NullSegGrp-{Guid.NewGuid().ToString()[..6]}";

            // Build test Excel file
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Grupos");
            ws.Cells[1, 1].Value = "Grupo";
            ws.Cells[1, 2].Value = "Segmento";

            // Row 2: existing group updated to Motores and re-activated
            ws.Cells[2, 1].Value = existingName;
            ws.Cells[2, 2].Value = "Motores";

            // Row 3: new group created with Serviços
            ws.Cells[3, 1].Value = newGroupName;
            ws.Cells[3, 2].Value = "Serviços";

            // Row 4: new group created with null segment
            ws.Cells[4, 1].Value = nullSegGroupName;
            ws.Cells[4, 2].Value = "";

            // Row 5: invalid segment
            ws.Cells[5, 1].Value = "InvalidSegGrp";
            ws.Cells[5, 2].Value = "SegmentoInvalido123";

            var fileBytes = package.GetAsByteArray();

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(fileContent, "file", "test_groups.xlsx");

            // Act
            var response = await client.PostAsync("/api/groups/import", form);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<GroupImportResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.UpdatedCount.Should().Be(1);
            result.Data.CreatedCount.Should().Be(2);
            result.Data.ErrorCount.Should().Be(1);
            result.Data.Errors[0].Error.Should().Contain("inválido");

            // Verify database state
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var updated = db.Groups.First(g => g.Name == existingName);
                updated.Segment.Should().Be(2); // Motores
                updated.IsActive.Should().BeTrue(); // Re-activated
                updated.Commission.Should().Be(20); // Preserved
                updated.Description.Should().Be("Original Desc"); // Preserved

                var created = db.Groups.First(g => g.Name == newGroupName);
                created.Segment.Should().Be(3); // Serviços
                created.IsActive.Should().BeTrue();

                var createdNull = db.Groups.First(g => g.Name == nullSegGroupName);
                createdNull.Segment.Should().BeNull();
                createdNull.IsActive.Should().BeTrue();
            }
        }
    }
}
