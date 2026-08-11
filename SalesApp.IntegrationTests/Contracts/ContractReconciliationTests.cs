using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using OfficeOpenXml;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Contracts Tests")]
    public class ContractReconciliationTests
    {
        private readonly ContractsTestFactory _factory;
        private readonly HttpClient _client;

        public ContractReconciliationTests(ContractsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
            ExcelPackage.License.SetNonCommercialOrganization("SalesAppTest");
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "superadmin@test.com",
                password = "superadmin123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task ReconcileContracts_ShouldIdentifyAllDiscrepanciesCorrectly()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var testUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Reconciliation Target User",
                Email = "reconcile_target@test.com",
                RoleId = 3,
                InternalId = 8881
            };

            var saleDate = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

            // DB Contracts:
            // 1. SYS-001 (R$ 1000) -> Matched in XLSX with same amount
            // 2. SYS-002 (R$ 2000) -> Matched in XLSX with DIFFERENT amount (R$ 2500)
            // 3. SYS-003 (R$ 3000) -> In System, but MISSING in XLSX
            var sysContract1 = new Contract
            {
                ContractNumber = "REC-SYS-001",
                TotalAmount = 1000.00m,
                UserInternalId = testUser.InternalId,
                SaleStartDate = saleDate,
                ContractStatusId = 1
            };

            var sysContract2 = new Contract
            {
                ContractNumber = "REC-SYS-002",
                TotalAmount = 2000.00m,
                UserInternalId = testUser.InternalId,
                SaleStartDate = saleDate,
                ContractStatusId = 1
            };

            var sysContract3 = new Contract
            {
                ContractNumber = "REC-SYS-003",
                TotalAmount = 3000.00m,
                UserInternalId = testUser.InternalId,
                SaleStartDate = saleDate,
                ContractStatusId = 1
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Users.Add(testUser);
                context.Contracts.AddRange(sysContract1, sysContract2, sysContract3);
                await context.SaveChangesAsync();
            }

            // Create XLSX file buffer:
            // Row 1: REC-SYS-001 (R$ 1000) -> Match
            // Row 2: REC-SYS-002 (R$ 2500) -> Mismatch
            // Row 3: REC-XLSX-999 (R$ 5000) -> Missing in System
            // Row 4: REC-UNASSIGNED-888 (R$ 7000) -> Unknown user email
            byte[] xlsxBytes;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Reconciliation");
                ws.Cells[1, 1].Value = "Contrato";
                ws.Cells[1, 2].Value = "Valor";
                ws.Cells[1, 3].Value = "Email";

                // Row 1
                ws.Cells[2, 1].Value = "REC-SYS-001";
                ws.Cells[2, 2].Value = 1000.00;
                ws.Cells[2, 3].Value = "reconcile_target@test.com";

                // Row 2
                ws.Cells[3, 1].Value = "REC-SYS-002";
                ws.Cells[3, 2].Value = 2500.00;
                ws.Cells[3, 3].Value = "reconcile_target@test.com";

                // Row 3
                ws.Cells[4, 1].Value = "REC-XLSX-999";
                ws.Cells[4, 2].Value = 5000.00;
                ws.Cells[4, 3].Value = "reconcile_target@test.com";

                // Row 4
                ws.Cells[5, 1].Value = "REC-UNASSIGNED-888";
                ws.Cells[5, 2].Value = 7000.00;
                ws.Cells[5, 3].Value = "unknown_non_existing_user@test.com";

                xlsxBytes = package.GetAsByteArray();
            }

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(xlsxBytes), "file", "reconciliation_test.xlsx");
            content.Add(new StringContent("2026-08-01"), "startDate");
            content.Add(new StringContent("2026-08-31"), "endDate");
            content.Add(new StringContent(testUser.Id.ToString()), "userId");

            // Act
            var response = await _client.PostAsync("/api/contractreconciliation/reconcile", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ContractReconciliationResultDto>();
            result.Should().NotBeNull();

            // Check Missing in System (REC-XLSX-999)
            result!.MissingInSystemSummary.Count.Should().Be(1);
            result.MissingInSystem[0].ContractNumber.Should().Be("REC-XLSX-999");
            result.MissingInSystem[0].TotalAmount.Should().Be(5000.00m);

            // Check Missing in Import (REC-SYS-003)
            result.MissingInImportSummary.Count.Should().Be(1);
            result.MissingInImport[0].ContractNumber.Should().Be("REC-SYS-003");
            result.MissingInImport[0].TotalAmount.Should().Be(3000.00m);

            // Check Amount Mismatch (REC-SYS-002: System 2000 vs XLSX 2500)
            result.AmountMismatchSummary.Count.Should().Be(1);
            result.AmountMismatches[0].ContractNumber.Should().Be("REC-SYS-002");
            result.AmountMismatches[0].SystemAmount.Should().Be(2000.00m);
            result.AmountMismatches[0].XlsxAmount.Should().Be(2500.00m);
            result.AmountMismatches[0].Difference.Should().Be(500.00m);

            // Check Unassigned User (REC-UNASSIGNED-888)
            result.UnassignedUserSummary.Count.Should().Be(1);
            result.UnassignedUserContracts[0].ContractNumber.Should().Be("REC-UNASSIGNED-888");
        }
    }
}
