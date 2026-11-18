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
    public class ImportAutoAssignMatriculaOwnerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly ImportsTestFactory _factory;

        public ImportAutoAssignMatriculaOwnerIntegrationTests(ImportsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task ImportDashboard_WhenNewContract_AndMatriculaOwnerHasAutoAssignTrue_AssignsContractToOwner()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var matriculaNumber = $"MAT-AUTO-{Guid.NewGuid().ToString("N")[..6]}";
            var contractNumber = $"CN-AUTO-{Guid.NewGuid().ToString("N")[..6]}";
            int ownerInternalId;

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var role = await ctx.Roles.FirstAsync(r => r.Name == "user");
                var ownerUser = new User
                {
                    Name = "Auto Owner",
                    Email = $"auto-owner-{Guid.NewGuid().ToString("N")[..6]}@test.com",
                    RoleId = role.Id,
                    PasswordHash = "hashed",
                    IsActive = true,
                    AutoAssignNewContractsToMatriculaOwner = true
                };
                ctx.Users.Add(ownerUser);
                await ctx.SaveChangesAsync();
                ownerInternalId = ownerUser.InternalId;

                var matricula = new Matricula
                {
                    MatriculaNumber = matriculaNumber,
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                ctx.Matriculas.Add(matricula);
                await ctx.SaveChangesAsync();

                ctx.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = ownerInternalId,
                    MatriculaId = matricula.Id,
                    IsOwner = true,
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "50000", "2026-02-01", matriculaNumber, contractNumber);

            confirmResult.Should().NotBeNull();
            confirmResult!.Success.Should().BeTrue();

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await ctx.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                contract.Should().NotBeNull();
                contract!.UserInternalId.Should().Be(ownerInternalId,
                    "new contract should be automatically assigned to the matricula owner when preference is true");
            }
        }

        [Fact]
        public async Task ImportDashboard_WhenNewContract_AndMatriculaOwnerHasAutoAssignFalse_LeavesContractUnassigned()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var matriculaNumber = $"MAT-NOAUTO-{Guid.NewGuid().ToString("N")[..6]}";
            var contractNumber = $"CN-NOAUTO-{Guid.NewGuid().ToString("N")[..6]}";

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var role = await ctx.Roles.FirstAsync(r => r.Name == "user");
                var ownerUser = new User
                {
                    Name = "No Auto Owner",
                    Email = $"no-auto-owner-{Guid.NewGuid().ToString("N")[..6]}@test.com",
                    RoleId = role.Id,
                    PasswordHash = "hashed",
                    IsActive = true,
                    AutoAssignNewContractsToMatriculaOwner = false // Disabled
                };
                ctx.Users.Add(ownerUser);
                await ctx.SaveChangesAsync();

                var matricula = new Matricula
                {
                    MatriculaNumber = matriculaNumber,
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                ctx.Matriculas.Add(matricula);
                await ctx.SaveChangesAsync();

                ctx.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = ownerUser.InternalId,
                    MatriculaId = matricula.Id,
                    IsOwner = true,
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "50000", "2026-02-01", matriculaNumber, contractNumber);

            confirmResult.Should().NotBeNull();
            confirmResult!.Success.Should().BeTrue();

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await ctx.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                contract.Should().NotBeNull();
                contract!.UserInternalId.Should().BeNull(
                    "new contract must remain unassigned when owner preference is false");
            }
        }

        [Fact]
        public async Task ImportDashboard_WhenExistingContractWithoutOwner_RemainsUntouched()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var matriculaNumber = $"MAT-EXIST-{Guid.NewGuid().ToString("N")[..6]}";
            var contractNumber = $"CN-EXIST-{Guid.NewGuid().ToString("N")[..6]}";

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var role = await ctx.Roles.FirstAsync(r => r.Name == "user");
                var ownerUser = new User
                {
                    Name = "Existing Test Owner",
                    Email = $"exist-owner-{Guid.NewGuid().ToString("N")[..6]}@test.com",
                    RoleId = role.Id,
                    PasswordHash = "hashed",
                    IsActive = true,
                    AutoAssignNewContractsToMatriculaOwner = true
                };
                ctx.Users.Add(ownerUser);
                await ctx.SaveChangesAsync();

                var matricula = new Matricula
                {
                    MatriculaNumber = matriculaNumber,
                    StartDate = DateTime.UtcNow,
                    Status = "active"
                };
                ctx.Matriculas.Add(matricula);
                await ctx.SaveChangesAsync();

                ctx.UserMatriculas.Add(new UserMatricula
                {
                    UserInternalId = ownerUser.InternalId,
                    MatriculaId = matricula.Id,
                    IsOwner = true,
                    IsActive = true
                });

                // Pre-existing contract with UserInternalId = null
                ctx.Contracts.Add(new Contract
                {
                    ContractNumber = contractNumber,
                    TotalAmount = 40000,
                    GroupId = 0,
                    ContractStatusId = 1,
                    IsActive = true,
                    MatriculaId = matricula.Id,
                    UserInternalId = null,
                    SaleStartDate = DateTime.UtcNow.AddMonths(-1)
                });
                await ctx.SaveChangesAsync();
            }

            var cota = $"G1;100;X;Customer;{contractNumber}";
            var (confirmResult, _) = await RunDashboardImport(
                cota, "60000", "2026-02-01", matriculaNumber, contractNumber);

            confirmResult.Should().NotBeNull();
            confirmResult!.Success.Should().BeTrue();

            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await ctx.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                contract.Should().NotBeNull();
                contract!.UserInternalId.Should().BeNull(
                    "existing contract must remain untouched without changing its user assignment");
            }
        }

        [Fact]
        public async Task PreferencesEndpoints_CanGetAndUpdateAutoAssignPreference()
        {
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Get current preferences
            var getRes = await _client.GetFromJsonAsync<ApiResponse<UserPreferencesResponse>>("/api/users/me/preferences");
            getRes.Should().NotBeNull();
            getRes!.Success.Should().BeTrue();

            // 2. Update to false
            var putRes = await _client.PutAsJsonAsync("/api/users/me/preferences", new UpdateUserPreferencesRequest
            {
                AutoAssignNewContractsToMatriculaOwner = false
            });
            putRes.EnsureSuccessStatusCode();
            var putData = await putRes.Content.ReadFromJsonAsync<ApiResponse<UserPreferencesResponse>>();
            putData!.Data!.AutoAssignNewContractsToMatriculaOwner.Should().BeFalse();

            // 3. Update back to true
            var putRes2 = await _client.PutAsJsonAsync("/api/users/me/preferences", new UpdateUserPreferencesRequest
            {
                AutoAssignNewContractsToMatriculaOwner = true
            });
            putRes2.EnsureSuccessStatusCode();
            var putData2 = await putRes2.Content.ReadFromJsonAsync<ApiResponse<UserPreferencesResponse>>();
            putData2!.Data!.AutoAssignNewContractsToMatriculaOwner.Should().BeTrue();
        }

        private async Task<(ApiResponse<ImportStatusResponse>? response, string uploadId)> RunDashboardImport(
            string cota, string total, string saleStartDate, string matricula, string contractNumber)
        {
            var csv = "Cota,Total,SaleStartDate,Matricula\n" +
                      $"{cota},{total},{saleStartDate},{matricula}";

            var uploadId = await UploadFile(csv, "dashboard.csv", templateId: 3);

            var mappings = new Dictionary<string, string>
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

            var mappingResponse = await _client.PostAsJsonAsync(
                $"/api/imports/{uploadId}/mappings",
                new { mappings, allowAutoCreateGroups = true });
            mappingResponse.EnsureSuccessStatusCode();

            var confirmResponse = await _client.PostAsJsonAsync(
                $"/api/imports/{uploadId}/confirm",
                new ConfirmImportRequest { AllowAutoCreateGroups = true, UpdateMatriculaOnExisting = true });
            confirmResponse.EnsureSuccessStatusCode();

            var result = await confirmResponse.Content
                .ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            return (result, uploadId);
        }

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
