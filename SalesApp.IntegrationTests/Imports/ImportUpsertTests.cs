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
            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
{contractNumber},superadmin@test.com,5000,0,late1,2024-01-01";

            var uploadId = await UploadFile(csvContent, "upsert.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
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

        [Fact]
        public async Task ImportContract_WithSujACancelamentoStatus_ShouldMapToLate3()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"STATUS-{Guid.NewGuid().ToString()[..8]}";

            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
{contractNumber},superadmin@test.com,5000,0,SUJ. A CANCELAMENTO,2024-01-01";

            var uploadId = await UploadFile(csvContent, "status-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
                }
            };

            // Act
            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                
                contract.Should().NotBeNull();
                contract!.Status.Should().Be("late3");
            }
        }

        [Fact]
        public async Task ImportContract_WithContNaoEntregueStatus_ShouldMapToLate2()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"STATUS-CNE-{Guid.NewGuid().ToString()[..8]}";

            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
{contractNumber},superadmin@test.com,5000,0,CONT NÃO ENTREGUE 2 ATR,2024-01-01";

            var uploadId = await UploadFile(csvContent, "status-cne-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
                }
            };

            // Act
            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                
                contract.Should().NotBeNull();
                contract!.Status.Should().Be("late2");
            }
        }

        [Fact]
        public async Task ImportContract_WithTransferidoStatus_ShouldMapToTransferred()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"STATUS-TRA-{Guid.NewGuid().ToString()[..8]}";

            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
{contractNumber},superadmin@test.com,5000,0,TRANSFERIDO,2024-01-01";

            var uploadId = await UploadFile(csvContent, "status-tra-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
                }
            };

            // Act
            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
                
                contract.Should().NotBeNull();
                contract!.Status.Should().Be("transferred");
            }
        }

        [Fact]
        public async Task ImportContract_WithMissingSaleStartDate_ShouldSkipSilently()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Row 1 is valid, Row 2 is missing SaleStartDate
            var contract1 = $"VALID-{Guid.NewGuid().ToString()[..8]}";
            var contract2 = $"MISSING-DATE-{Guid.NewGuid().ToString()[..8]}";

            var csvContent = $@"Contract Number,User Email,Total Amount,Group,Status,Sale Start Date
{contract1},superadmin@test.com,5000,0,Active,2024-01-01
{contract2},superadmin@test.com,5000,0,Active,";

            var uploadId = await UploadFile(csvContent, "skip-date-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "Sale Start Date", "SaleStartDate" }
                }
            };

            // Act - Step 1: Mappings (Validation)
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty(); // NO ERROR for Row 2

            // Act - Step 2: Confirm (Execution)
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            confirmResult!.Data!.ProcessedRows.Should().Be(1); // Only Row 1 processed
            confirmResult.Data.FailedRows.Should().Be(0); // Row 2 skipped silently, not marked as failed

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                // Contract 1 should exist
                var c1 = await context.Contracts.AnyAsync(c => c.ContractNumber == contract1);
                c1.Should().BeTrue();

                // Contract 2 should NOT exist
                var c2 = await context.Contracts.AnyAsync(c => c.ContractNumber == contract2);
                c2.Should().BeFalse();
            }
        }

        [Fact]
        public async Task ConfigureMappings_WithSkipMissingContractNumber_ShouldAllowEmptyContractNumbers()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Row 1 has contract number, Row 2 does NOT
            var csvContent = @"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
C-123,superadmin@test.com,5000,0,Active,2024-01-01
,superadmin@test.com,5000,0,Active,2024-01-01";

            var uploadId = await UploadFile(csvContent, "skip-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
                },
                skipMissingContractNumber = true // THE FLAG WE WANT TO TEST
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            result!.Success.Should().BeTrue();
            result.Data!.Errors.Should().BeEmpty(); // Should NOT have "Missing required value" for Row 2
        }

        [Fact]
        public async Task ConfigureMappings_WithoutSkipMissingContractNumber_ShouldFailOnEmptyContractNumbers()
        {
            // Arrange
            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Row 1 has contract number, Row 2 does NOT
            var csvContent = @"Contract Number,User Email,Total Amount,Group,Status,SaleStartDate
C-123,superadmin@test.com,5000,0,Active,2024-01-01
,superadmin@test.com,5000,0,Active,2024-01-01";

            var uploadId = await UploadFile(csvContent, "fail-test.csv");

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Contract Number", "ContractNumber" },
                    { "User Email", "UserEmail" },
                    { "Total Amount", "TotalAmount" },
                    { "Group", "GroupId" },
                    { "Status", "Status" },
                    { "SaleStartDate", "SaleStartDate" }
                },
                skipMissingContractNumber = false // DEFAULT OR EXPLICIT FALSE
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            result!.Success.Should().BeTrue();
            result.Data!.Errors.Should().Contain(e => e.Contains("Missing required value for field: ContractNumber"));
        }
        [Fact]
        public async Task ImportContractDashboard_WithInvalidCota_ShouldSkipSilently()
        {
            // Arrange
            var csv = "Cota,Total,SaleStartDate\n" +
                      "G1;123;C1;Cust1;CNT-VALID,1000,2024-01-01\n" + // VALID (5 parts, with numeric quota)
                      "INVALID-COTA,1000,2024-01-01\n" +            // INVALID (no semicolons)
                      ",1000,2024-01-01";                             // BLANK

            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = await UploadFile(csv, "dashboard_skip.csv", templateId: 3);

            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = new Dictionary<string, string>
                {
                    { "Cota", "Cota" },
                    { "Total", "TotalAmount" },
                    { "SaleStartDate", "SaleStartDate" },
                    { "cota.contract", "ContractNumber" }
                }
            };

            // Act - Step 1: Validation
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty(); // ALL skipped or valid

            // Act - Step 2: Confirm
            var confirmRequest = new ConfirmImportRequest { DateFormat = "YYYY-MM-DD", AllowAutoCreateGroups = true };
            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/confirm", confirmRequest);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            confirmResult!.Data!.ProcessedRows.Should().Be(1); // Only Row 1
            confirmResult.Data.FailedRows.Should().Be(0); // Row 2 & 3 skipped silently
        }

        [Fact]
        public async Task ImportContractDashboard_WithSujACancelamentoStatus_ShouldMapToLate3()
        {
            // Arrange
            var csv = "Cota,Total,SaleStartDate,Status\n" +
                      "G1;123;C1;Cust1;CNT-SUJ-CANCEL,1000,2024-01-01,SUJ. A CANCELAMENTO";

            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = await UploadFile(csv, "dashboard_status.csv", templateId: 3);

            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = new Dictionary<string, string>
                {
                    { "Cota", "Cota" },
                    { "Total", "TotalAmount" },
                    { "SaleStartDate", "SaleStartDate" },
                    { "Status", "Status" }
                }
            };

            // Act - Step 1: Mapping/Validation
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings?entityType=Contract", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty();

            // Act - Step 2: Confirm
            var confirmRequest = new ConfirmImportRequest { DateFormat = "YYYY-MM-DD", AllowAutoCreateGroups = true };
            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/confirm", confirmRequest);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rawConfirm = await confirmResponse.Content.ReadAsStringAsync();
            confirmResult!.Data!.ProcessedRows.Should().Be(1, because: $"Raw Confirm: {rawConfirm}");
            confirmResult.Data.FailedRows.Should().Be(0, because: $"Raw Confirm: {rawConfirm}");

            // Verify status in DB
            var contractsResponse = await _client.GetAsync("/api/contracts?contractNumber=CNT-SUJ-CANCEL");
            var rawSearch = await contractsResponse.Content.ReadAsStringAsync();
            var contractsResult = await contractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            contractsResult!.Data.Should().NotBeEmpty(because: $"Contracts for CNT-SUJ-CANCEL were not found. Raw Response: {rawSearch} | Raw Confirm: {rawConfirm}");
            contractsResult.Data!.First().Status.Should().Be("late3");
        }

        [Fact]
        public async Task ImportContractDashboard_WithContNaoEntregueStatus_ShouldMapToLate2()
        {
            // Arrange
            var csv = "Cota,Total,SaleStartDate,Status\n" +
                      "G1;123;C1;Cust1;CNT-CONT-NAO-ENT,1000,2024-01-01,CONT NÃO ENTREGUE 2 ATR";

            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = await UploadFile(csv, "dashboard_status_late2.csv", templateId: 3);

            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = new Dictionary<string, string>
                {
                    { "Cota", "Cota" },
                    { "Total", "TotalAmount" },
                    { "SaleStartDate", "SaleStartDate" },
                    { "Status", "Status" }
                }
            };

            // Act - Step 1: Mapping/Validation
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings?entityType=Contract", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty();

            // Act - Step 2: Confirm
            var confirmRequest = new ConfirmImportRequest { DateFormat = "YYYY-MM-DD", AllowAutoCreateGroups = true };
            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/confirm", confirmRequest);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            confirmResult!.Data!.ProcessedRows.Should().Be(1);

            // Verify status in DB
            var contractsResponse = await _client.GetAsync("/api/contracts?contractNumber=CNT-CONT-NAO-ENT");
            var contractsResult = await contractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            contractsResult!.Data.Should().NotBeEmpty();
            contractsResult.Data!.First().Status.Should().Be("late2");
        }

        [Fact]
        public async Task ImportContractDashboard_WithCont1AtrStatus_ShouldMapToLate1()
        {
            // Arrange
            var csv = "Cota,Total,SaleStartDate,Status\n" +
                      "G1;123;C1;Cust1;CNT-CONT-1-ATR,1000,2024-01-01,CONT 1 ATR";

            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = await UploadFile(csv, "dashboard_status_late1.csv", templateId: 3);

            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = new Dictionary<string, string>
                {
                    { "Cota", "Cota" },
                    { "Total", "TotalAmount" },
                    { "SaleStartDate", "SaleStartDate" },
                    { "Status", "Status" }
                }
            };

            // Act - Step 1: Mapping/Validation
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings?entityType=Contract", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty();

            // Act - Step 2: Confirm
            var confirmRequest = new ConfirmImportRequest { DateFormat = "YYYY-MM-DD", AllowAutoCreateGroups = true };
            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/confirm", confirmRequest);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            confirmResult!.Data!.ProcessedRows.Should().Be(1);

            // Verify status in DB
            var contractsResponse = await _client.GetAsync("/api/contracts?contractNumber=CNT-CONT-1-ATR");
            var contractsResult = await contractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            contractsResult!.Data.Should().NotBeEmpty();
            contractsResult.Data!.First().Status.Should().Be("late1");
        }

        [Fact]
        public async Task ImportContractDashboard_WithContBemPend2AtrStatus_ShouldMapToLate2()
        {
            // Arrange
            var csv = "Cota,Total,SaleStartDate,Status\n" +
                      "G1;123;C1;Cust1;CNT-CONT-BEM-PEND,1000,2024-01-01,CONT BEM PEND 2 ATR";

            var token = await GetSuperAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uploadId = await UploadFile(csv, "dashboard_status_late2_bem_pend.csv", templateId: 3);

            var mappingRequest = new ColumnMappingRequest
            {
                Mappings = new Dictionary<string, string>
                {
                    { "Cota", "Cota" },
                    { "Total", "TotalAmount" },
                    { "SaleStartDate", "SaleStartDate" },
                    { "Status", "Status" }
                }
            };

            // Act - Step 1: Mapping/Validation
            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings?entityType=Contract", mappingRequest);
            var mappingResult = await mappingResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Validation
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            mappingResult!.Data!.Errors.Should().BeEmpty();

            // Act - Step 2: Confirm
            var confirmRequest = new ConfirmImportRequest { DateFormat = "YYYY-MM-DD", AllowAutoCreateGroups = true };
            var confirmResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/confirm", confirmRequest);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();

            // Assert Execution
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            confirmResult!.Data!.ProcessedRows.Should().Be(1);

            // Verify status in DB
            var contractsResponse = await _client.GetAsync("/api/contracts?contractNumber=CNT-CONT-BEM-PEND");
            var contractsResult = await contractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            contractsResult!.Data.Should().NotBeEmpty();
            contractsResult.Data!.First().Status.Should().Be("late2");
        }

        private async Task<string> UploadFile(string content, string fileName, int templateId = 2)
        {
            var multipartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipartContent.Add(fileContent, "file", fileName);

            var response = await _client.PostAsync($"/api/imports/upload?templateId={templateId}", multipartContent);
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
