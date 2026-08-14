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
    public class UpdateStartDateDashboardImportTests
    {
        private readonly ImportsTestFactory _factory;

        public UpdateStartDateDashboardImportTests(ImportsTestFactory factory)
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
                FileName = "dashboard_startdate_test.csv",
                Status = "preview",
                UploadedByUserInternalId = admin?.InternalId ?? 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var group = new Group
            {
                Name = $"StartDateGroup-{Guid.NewGuid().ToString("N")[..8]}",
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
        public async Task Import_ExistingContract_UpdateStartDateDisabled_ShouldNotUpdateSaleStartDate()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var (session, group) = await SetupAsync(context, groupRepo, uploadId);

            var contractNumber = $"CN-STARTDATE-OFF-{Guid.NewGuid().ToString("N")[..6]}";
            var originalDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            var activeStatus = await context.ContractStatuses.FirstAsync(s => s.Name == ContractStatus.Active.ToApiString());

            var existingContract = new Contract
            {
                ContractNumber = contractNumber,
                CustomerName = "Existing Customer",
                GroupId = group.Id,
                Quota = 10,
                TotalAmount = 50000m,
                SaleStartDate = originalDate,
                ContractStatusId = activeStatus.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Contracts.Add(existingContract);
            await context.SaveChangesAsync();

            var row = new Dictionary<string, string>
            {
                { "ContractNumber", contractNumber },
                { "GroupId", group.Id.ToString() },
                { "Quota", "10" },
                { "CustomerName", "Existing Customer" },
                { "TotalAmount", "50000" },
                { "SaleStartDate", "2025-06-20" }, // New date in file
                { "Status", "Ativo" }
            };

            await service.ExecuteContractDashboardImportAsync(
                uploadId, session.Id, new List<Dictionary<string, string>> { row }, BuildMappings(),
                updateStartDateOnExisting: false);

            var updatedContract = await context.Contracts.FirstAsync(c => c.ContractNumber == contractNumber);
            updatedContract.SaleStartDate.Should().Be(originalDate);
        }

        [Fact]
        public async Task Import_ExistingContract_UpdateStartDateEnabled_ShouldUpdateSaleStartDate()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var (session, group) = await SetupAsync(context, groupRepo, uploadId);

            var contractNumber = $"CN-STARTDATE-ON-{Guid.NewGuid().ToString("N")[..6]}";
            var originalDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var expectedNewDate = new DateTime(2025, 6, 20, 0, 0, 0, DateTimeKind.Unspecified);

            var activeStatus = await context.ContractStatuses.FirstAsync(s => s.Name == ContractStatus.Active.ToApiString());

            var existingContract = new Contract
            {
                ContractNumber = contractNumber,
                CustomerName = "Existing Customer",
                GroupId = group.Id,
                Quota = 10,
                TotalAmount = 50000m,
                SaleStartDate = originalDate,
                ContractStatusId = activeStatus.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Contracts.Add(existingContract);
            await context.SaveChangesAsync();

            var row = new Dictionary<string, string>
            {
                { "ContractNumber", contractNumber },
                { "GroupId", group.Id.ToString() },
                { "Quota", "10" },
                { "CustomerName", "Existing Customer" },
                { "TotalAmount", "50000" },
                { "SaleStartDate", "2025-06-20" }, // New date in file
                { "Status", "Ativo" }
            };

            await service.ExecuteContractDashboardImportAsync(
                uploadId, session.Id, new List<Dictionary<string, string>> { row }, BuildMappings(),
                updateStartDateOnExisting: true);

            var updatedContract = await context.Contracts.FirstAsync(c => c.ContractNumber == contractNumber);
            updatedContract.SaleStartDate.Should().Be(expectedNewDate);
        }
    }
}
