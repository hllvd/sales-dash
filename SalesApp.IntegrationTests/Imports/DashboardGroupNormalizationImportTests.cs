using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Imports Tests")]
    public class DashboardGroupNormalizationImportTests
    {
        private readonly ImportsTestFactory _factory;

        public DashboardGroupNormalizationImportTests(ImportsTestFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task DashboardImport_WithPaddedGroupNumbers_NormalizesGroupAndUpdatesExistingContracts()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var importService = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();

            var suffix = Guid.NewGuid().ToString("N")[..6];
            var contractNum = $"CNT-DASH-{suffix}";

            var session = new ImportSession
            {
                UploadId = $"dash-{suffix}",
                FileName = "dash_test.xlsx",
                Status = "Processing",
                UploadedByUserInternalId = 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var mappings = new Dictionary<string, string>
            {
                { "Contrato", "ContractNumber" },
                { "Grupo", "GroupId" },
                { "Cota", "Quota" },
                { "Cliente", "CustomerName" },
                { "Valor", "TotalAmount" },
                { "Data", "SaleStartDate" },
                { "Situacao", "Status" }
            };

            // First import: row with leading zeros "000560"
            var rows1 = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Contrato", contractNum },
                    { "Grupo", "000560" },
                    { "Cota", "123" },
                    { "Cliente", "Cliente Teste" },
                    { "Valor", "50000" },
                    { "Data", "2026-05-10" },
                    { "Situacao", "Ativo" }
                }
            };

            var res1 = await importService.ExecuteContractDashboardImportAsync(
                uploadId: session.UploadId,
                importSessionId: session.Id,
                rows: rows1,
                mappings: mappings,
                allowAutoCreateGroups: true
            );

            res1.ProcessedRows.Should().Be(1);

            // Verify group created with normalized name "560", NOT "000560"
            var groupZero = await context.Groups.FirstOrDefaultAsync(g => g.Name == "000560");
            groupZero.Should().BeNull("Group should never be created with leading zeros");

            var groupNorm = await context.Groups.FirstOrDefaultAsync(g => g.Name == "560");
            groupNorm.Should().NotBeNull("Group should have been normalized to 560");

            var contract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNum);
            contract.Should().NotBeNull();
            contract!.GroupId.Should().Be(groupNorm!.Id);

            // Second import: re-import same contract with another group with zeros: "000720"
            var rows2 = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Contrato", contractNum },
                    { "Grupo", "000720" },
                    { "Cota", "123" },
                    { "Cliente", "Cliente Teste" },
                    { "Valor", "50000" },
                    { "Data", "2026-05-10" },
                    { "Situacao", "Ativo" }
                }
            };

            var res2 = await importService.ExecuteContractDashboardImportAsync(
                uploadId: session.UploadId,
                importSessionId: session.Id,
                rows: rows2,
                mappings: mappings,
                allowAutoCreateGroups: true
            );

            res2.ProcessedRows.Should().Be(1);

            context.ChangeTracker.Clear();

            var groupNew = await context.Groups.FirstOrDefaultAsync(g => g.Name == "720");
            groupNew.Should().NotBeNull("New group should have been created as 720");

            var updatedContract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNum);
            updatedContract!.GroupId.Should().Be(groupNew!.Id, "Existing contract should always be updated with the normalized group from latest import");
        }
    }
}
