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
    /// <summary>
    /// Integration tests for matricula change detection during contractDashboard imports.
    ///
    /// All tests use the established Cota format: "Group;Quota;X;Customer;ContractNumber"
    /// which provides GroupId and Quota required by BuildContractDashboardFromRowAsync.
    /// Template ID 3 = contractDashboard.
    /// </summary>
    [Collection("Integration Tests")]
    public class ImportDashboardMatriculaChangeTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ImportDashboardMatriculaChangeTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        // ── Test 1: No change when matricula is the same ──────────────────────

        [Fact]
        public async Task ImportDashboard_WhenMatriculaUnchanged_ShouldNotReportChange()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"MC-SAME-{Guid.NewGuid().ToString()[..6]}";
            var matriculaNumber = $"MAT-SAME-{Guid.NewGuid().ToString()[..6]}";

            // Seed: contract already linked to matriculaNumber
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var mat = new Matricula { MatriculaNumber = matriculaNumber, StartDate = DateTime.UtcNow, Status = "active" };
                ctx.Matriculas.Add(mat);
                await ctx.SaveChangesAsync();

                ctx.Contracts.Add(new Contract
                {
                    ContractNumber = contractNumber,
                    TotalAmount = 1000,
                    GroupId = 0,
                    ContractStatusId = 1,
                    IsActive = true,
                    MatriculaId = mat.Id,
                    SaleStartDate = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
            }

            // Import same contract with the SAME matricula
            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "1000", "2024-01-01", matriculaNumber, contractNumber);

            // Assert: no changes
            confirmResult.Should().NotBeNull();
            confirmResult!.Data!.MatriculaChanges.Should().BeEmpty();
            confirmResult.Data.Warnings.Should().NotContain(w => w.Contains("alteração de matrículas"));
        }

        // ── Test 2: Change detected, contract MatriculaId updated ─────────────

        [Fact]
        public async Task ImportDashboard_WhenMatriculaChanges_ShouldUpdateContractAndReportChange()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"MC-CHG-{Guid.NewGuid().ToString()[..6]}";
            var matriculaA     = $"MAT-A-{Guid.NewGuid().ToString()[..6]}";
            var matriculaB     = $"MAT-B-{Guid.NewGuid().ToString()[..6]}";

            // Seed: contract linked to MatriculaA; MatriculaB already exists
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var matA = new Matricula { MatriculaNumber = matriculaA, StartDate = DateTime.UtcNow, Status = "active" };
                var matB = new Matricula { MatriculaNumber = matriculaB, StartDate = DateTime.UtcNow, Status = "active" };
                ctx.Matriculas.AddRange(matA, matB);
                await ctx.SaveChangesAsync();

                ctx.Contracts.Add(new Contract
                {
                    ContractNumber = contractNumber,
                    TotalAmount = 1000,
                    GroupId = 0,
                    ContractStatusId = 1,
                    IsActive = true,
                    MatriculaId = matA.Id,
                    SaleStartDate = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
            }

            // Import same contract with MatriculaB
            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "2000", "2024-01-01", matriculaB, contractNumber);

            // Assert: structured change entry
            confirmResult.Should().NotBeNull();
            confirmResult!.Data!.MatriculaChanges.Should().HaveCount(1);
            var change = confirmResult.Data.MatriculaChanges[0];
            change.ContractNumber.Should().Be(contractNumber);
            change.OldMatricula.Should().Be(matriculaA);
            change.NewMatricula.Should().Be(matriculaB);

            // Assert: human-readable warning
            confirmResult.Data.Warnings.Should().Contain(w =>
                w.Contains("alteração de matrículas") &&
                w.Contains(contractNumber) &&
                w.Contains(matriculaB));

            // Assert: DB reflects new matricula
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await ctx.Contracts
                    .Include(c => c.Matricula)
                    .FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                contract.Should().NotBeNull();
                contract!.Matricula!.MatriculaNumber.Should().Be(matriculaB);
            }
        }

        // ── Test 3: Existing user gets UserMatricula link to new matricula ────

        [Fact]
        public async Task ImportDashboard_WhenMatriculaChanges_ShouldLinkExistingUserToNewMatricula()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"MC-LNK-{Guid.NewGuid().ToString()[..6]}";
            var matriculaA     = $"MAT-LNK-A-{Guid.NewGuid().ToString()[..6]}";
            var matriculaB     = $"MAT-LNK-B-{Guid.NewGuid().ToString()[..6]}";

            Guid assignedUserId;
            int matBId;

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await ctx.Users.FirstAsync(u => u.Email == "user@test.com");
                assignedUserId = user.Id;

                var matA = new Matricula { MatriculaNumber = matriculaA, StartDate = DateTime.UtcNow, Status = "active" };
                var matB = new Matricula { MatriculaNumber = matriculaB, StartDate = DateTime.UtcNow, Status = "active" };
                ctx.Matriculas.AddRange(matA, matB);
                await ctx.SaveChangesAsync();

                matBId = matB.Id;

                ctx.Contracts.Add(new Contract
                {
                    ContractNumber = contractNumber,
                    TotalAmount = 1000,
                    GroupId = 0,
                    ContractStatusId = 1,
                    IsActive = true,
                    MatriculaId = matA.Id,
                    UserInternalId = user.InternalId,
                    SaleStartDate = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
            }

            // Import with MatriculaB
            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "2000", "2024-01-01", matriculaB, contractNumber);

            confirmResult!.Data!.MatriculaChanges.Should().HaveCount(1);

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // UserId must NOT change
                var contract = await ctx.Contracts.Include(c => c.User).FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                contract!.User?.Id.Should().Be(assignedUserId,
                    because: "UserId is never updated on matricula change");

                // UserMatricula link to MatriculaB must now exist for the assigned user
                var link = await ctx.UserMatriculas.FirstOrDefaultAsync(
                    um => um.User.Id == assignedUserId && um.MatriculaId == matBId);
                link.Should().NotBeNull(
                    because: "the existing user should be linked to the new matricula");
            }
        }

        // ── Test 4: Insert path — change detection must not fire ─────────────

        [Fact]
        public async Task ImportDashboard_Insert_ShouldNotTriggerChangeDetection()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"MC-NEW-{Guid.NewGuid().ToString()[..6]}";
            var matriculaB     = $"MAT-NEW-B-{Guid.NewGuid().ToString()[..6]}";

            // Brand-new contract — no existing record in DB
            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "5000", "2024-01-01", matriculaB, contractNumber);

            confirmResult!.Data!.ProcessedRows.Should().Be(1,
                because: "a new contract with complete data should be inserted");
            confirmResult.Data.MatriculaChanges.Should().BeEmpty(
                because: "change detection only applies to updates, not inserts");
            confirmResult.Data.Warnings.Should().NotContain(w => w.Contains("alteração de matrículas"));
        }

        // ── Test 5: Multiple contracts change in one import ───────────────────

        [Fact]
        public async Task ImportDashboard_WhenMultipleContractsChange_ShouldReportAll()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var suffix    = Guid.NewGuid().ToString()[..5];
            var contract1 = $"MC-MULTI-1-{suffix}";
            var contract2 = $"MC-MULTI-2-{suffix}";
            var matA1     = $"MAT-M-A1-{suffix}";
            var matA2     = $"MAT-M-A2-{suffix}";
            var matB      = $"MAT-M-B-{suffix}";

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var mA1 = new Matricula { MatriculaNumber = matA1, StartDate = DateTime.UtcNow, Status = "active" };
                var mA2 = new Matricula { MatriculaNumber = matA2, StartDate = DateTime.UtcNow, Status = "active" };
                var mB  = new Matricula { MatriculaNumber = matB,  StartDate = DateTime.UtcNow, Status = "active" };
                ctx.Matriculas.AddRange(mA1, mA2, mB);
                await ctx.SaveChangesAsync();

                ctx.Contracts.AddRange(
                    new Contract
                    {
                        ContractNumber = contract1, TotalAmount = 1000, GroupId = 0,
                        ContractStatusId = 1, IsActive = true, MatriculaId = mA1.Id,
                        SaleStartDate = DateTime.UtcNow
                    },
                    new Contract
                    {
                        ContractNumber = contract2, TotalAmount = 2000, GroupId = 0,
                        ContractStatusId = 1, IsActive = true, MatriculaId = mA2.Id,
                        SaleStartDate = DateTime.UtcNow
                    }
                );
                await ctx.SaveChangesAsync();
            }

            // Both contracts move to matB in one CSV
            var csv = "Cota,Total,SaleStartDate,Matricula\n" +
                      $"G1;100;X;Customer1;{contract1},1000,2024-01-01,{matB}\n" +
                      $"G1;101;X;Customer2;{contract2},2000,2024-01-01,{matB}";

            var uploadId = await UploadFile(csv, "multi-change.csv", templateId: 3);

            var mappings = BuildCotaMappings();
            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", new { mappings, allowAutoCreateGroups = true });

            var confirmResponse = await _client.PostAsJsonAsync(
                $"/api/imports/{uploadId}/confirm",
                new ConfirmImportRequest { AllowAutoCreateGroups = true });
            var confirmResult = await confirmResponse.Content
                .ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            confirmResult!.Data!.MatriculaChanges.Should().HaveCount(2);
            confirmResult.Data.MatriculaChanges
                .Select(c => c.ContractNumber)
                .Should().Contain(new[] { contract1, contract2 });
            confirmResult.Data.MatriculaChanges
                .Should().AllSatisfy(c => c.NewMatricula.Should().Be(matB));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Uploads a single-row dashboard CSV using Cota format, maps columns,
        /// confirms, and returns the deserialized response.
        /// </summary>
        private async Task<(ApiResponse<ImportStatusResponse>? response, string uploadId)> RunDashboardImport(
            string cota, string total, string saleStartDate, string matricula, string contractNumber)
        {
            var csv = "Cota,Total,SaleStartDate,Matricula\n" +
                      $"{cota},{total},{saleStartDate},{matricula}";

            var uploadId = await UploadFile(csv, "dashboard.csv", templateId: 3);

            var mappings = BuildCotaMappings();
            var mappingResponse = await _client.PostAsJsonAsync(
                $"/api/imports/{uploadId}/mappings",
                new { mappings, allowAutoCreateGroups = true });
            mappingResponse.EnsureSuccessStatusCode();

            var confirmResponse = await _client.PostAsJsonAsync(
                $"/api/imports/{uploadId}/confirm",
                new ConfirmImportRequest { AllowAutoCreateGroups = true });
            confirmResponse.EnsureSuccessStatusCode();

            var result = await confirmResponse.Content
                .ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            return (result, uploadId);
        }

        private static Dictionary<string, string> BuildCotaMappings() => new()
        {
            { "Cota",         "Cota"           },
            { "cota.contract","ContractNumber"  },
            { "cota.group",   "GroupId"         },
            { "cota.cota",    "Quota"           },
            { "cota.customer","CustomerName"    },
            { "Total",        "TotalAmount"     },
            { "SaleStartDate","SaleStartDate"   },
            { "Matricula",    "MatriculaNumber" }
        };

        private async Task<string> UploadFile(string content, string fileName, int templateId = 3)
        {
            var multipart = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipart.Add(fileContent, "file", fileName);

            var response = await _client.PostAsync($"/api/imports/upload?templateId={templateId}", multipart);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!.UploadId;
        }

        private async Task<string> GetSuperAdminToken()
        {
            var response = await _client.PostAsJsonAsync("/api/users/login",
                new LoginRequest { Email = "superadmin@test.com", Password = "superadmin123" });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }
    }
}
