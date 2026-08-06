using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Imports Tests")]
    public class MissingTotalAmountDashboardImportTests
    {
        private readonly ImportsTestFactory _factory;

        public MissingTotalAmountDashboardImportTests(ImportsTestFactory factory)
        {
            _factory = factory;
        }

        private async Task<(ImportSession session, Group group)> SetupAsync(
            AppDbContext context, IGroupRepository groupRepo, string uploadId)
        {
            var admin = await context.Users.FirstOrDefaultAsync(u => u.Role.Name == "superadmin");
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "dashboard_test.csv",
                Status = "preview",
                UploadedByUserInternalId = admin?.InternalId ?? 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var group = new Group
            {
                Name = $"MissingAmountGroup-{Guid.NewGuid().ToString("N")[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            return (session, group);
        }

        private static Dictionary<string, string> BuildMappings() => new()
        {
            { "ContractNumber", "ContractNumber" },
            { "GroupId",        "GroupId" },
            { "Quota",          "Quota" },
            { "CustomerName",   "CustomerName" },
            { "TotalAmount",   "TotalAmount" },
            { "SaleStartDate", "SaleStartDate" },
            { "Status",        "Status" }
        };

        [Fact]
        public async Task Import_NewContract_MissingTotalAmount_ShouldSkipContractAndAddWarning()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var (session, group) = await SetupAsync(context, groupRepo, uploadId);

            var contractNumber = $"CN-MISSING-NEW-{Guid.NewGuid().ToString("N")[..6]}";

            var row = new Dictionary<string, string>
            {
                { "ContractNumber", contractNumber },
                { "GroupId", group.Id.ToString() },
                { "Quota", "101" },
                { "CustomerName", "Test User" },
                { "TotalAmount", "" }, // Missing!
                { "SaleStartDate", "2025-01-01" },
                { "Status", "Ativo" }
            };

            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId, session.Id, new List<Dictionary<string, string>> { row }, BuildMappings(),
                updateTotalAmountOnExisting: true);

            // Assert contract was NOT created
            var createdContract = await context.Contracts.FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
            createdContract.Should().BeNull();

            // Assert result metrics
            result.FailedRows.Should().Be(0);
            result.Warnings.Should().Contain(w => w.Contains("Não criaremos estes contratos porque a Ava Pro não nos fornece o valor de `Crédito Venda`") && w.Contains(contractNumber));
        }

        [Fact]
        public async Task Import_ExistingContract_MissingTotalAmount_UpdateTotalAmountOn_ShouldUpdateStatusAndAddWarning()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var statusService = scope.ServiceProvider.GetRequiredService<IContractStatusService>();

            var uploadId = Guid.NewGuid().ToString();
            var (session, group) = await SetupAsync(context, groupRepo, uploadId);

            var contractNumber = $"CN-MISSING-EXISTING-{Guid.NewGuid().ToString("N")[..6]}";
            var activeStatusId = await statusService.GetStatusIdByNameAsync("Active");
            var defaultedStatusId = await statusService.GetStatusIdByNameAsync("Defaulted");

            // Seed existing contract with TotalAmount = 50000 and Active status
            var existingContract = new Contract
            {
                ContractNumber = contractNumber,
                GroupId = group.Id,
                Quota = 102,
                CustomerName = "Existing User",
                TotalAmount = 50000m,
                ContractStatusId = activeStatusId,
                SaleStartDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Contracts.Add(existingContract);
            await context.SaveChangesAsync();

            var row = new Dictionary<string, string>
            {
                { "ContractNumber", contractNumber },
                { "GroupId", group.Id.ToString() },
                { "Quota", "102" },
                { "CustomerName", "Existing User" },
                { "TotalAmount", "" }, // Missing in source!
                { "Status", "Cancelado" } // Cancelado maps to Defaulted in configuration
            };

            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId, session.Id, new List<Dictionary<string, string>> { row }, BuildMappings(),
                updateTotalAmountOnExisting: true);

            // Fetch contract from DB to verify
            var dbContract = await context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.ContractNumber == contractNumber);
            dbContract.Should().NotBeNull();
            dbContract!.ContractStatusId.Should().Be(defaultedStatusId); // Status updated to Defaulted (mapped from "Cancelado")!
            dbContract.TotalAmount.Should().Be(50000m); // TotalAmount unchanged!

            // Assert warning
            result.FailedRows.Should().Be(0);
            result.Warnings.Should().Contain(w => w.Contains("Não foi possível atualizar a coluna de Valor Total para estes contratos porque a Ava Pro não nos fornece o valor de `Crédito Venda`") && w.Contains(contractNumber));
        }
    }
}
