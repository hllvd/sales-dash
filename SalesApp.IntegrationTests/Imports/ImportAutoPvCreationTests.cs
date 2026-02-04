using Microsoft.Extensions.DependencyInjection;
using SalesApp.Models;
using SalesApp.Services;
using SalesApp.Data;
using SalesApp.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class ImportAutoPvCreationTests
    {
        private readonly TestWebApplicationFactory _factory;

        public ImportAutoPvCreationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ImportContractDashboard_WithAutoCreatePVEnabled_ShouldCreatePV()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var pvId = 12345;
            var pvName = $"New PV {Guid.NewGuid().ToString()[..8]}";

            // Verify PV doesn't exist
            var existingPv = await context.PVs.FirstOrDefaultAsync(p => p.Id == pvId || p.Name == pvName);
            existingPv.Should().BeNull();

            // Create test user (needed for contracts to be correctly built, though dashboard import usually handles it differently if it uses TempMatricula, but here we test the general logic)
            // Actually, dashboard import in ImportExecutionService uses ResolveGroupIdAsync which might create groups.
            // Let's use a dashboard row format.

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "TotalAmount", "1000.00" },
                    { "SaleStartDate", "2024-01-01" },
                    { "GroupId", "9999" }, // Dummy group
                    { "Quota", "1" },
                    { "CustomerName", "Test Customer" },
                    { "PvId", pvId.ToString() },
                    { "PvName", pvName }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "TotalAmount", "TotalAmount" },
                { "SaleStartDate", "SaleStartDate" },
                { "GroupId", "GroupId" },
                { "Quota", "Quota" },
                { "CustomerName", "CustomerName" },
                { "PvId", "PvId" },
                { "PvName", "PvName" }
            };

            // Act
            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId, 
                rows, 
                mappings, 
                skipMissingContractNumber: false, 
                allowAutoCreateGroups: true, // Needed for 9999
                allowAutoCreatePVs: true);

            // Assert
            if (result.FailedRows > 0)
            {
                throw new Xunit.Sdk.XunitException($"Import failed with errors: {string.Join(", ", result.Errors)}");
            }
            result.ProcessedRows.Should().Be(1);
            result.CreatedPVs.Should().Contain(pvName);

            // Verify PV was created in DB
            var newPv = await context.PVs.FirstOrDefaultAsync(p => p.Name == pvName);
            newPv.Should().NotBeNull();
            newPv!.Name.Should().Be(pvName);
        }

        [Fact]
        public async Task ImportContractDashboard_WithAutoCreatePVDisabled_ShouldNotCreatePV()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var uploadId = Guid.NewGuid().ToString();
            var pvId = 54321;
            var pvName = $"Another New PV {Guid.NewGuid().ToString()[..8]}";

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "TotalAmount", "1000.00" },
                    { "SaleStartDate", "2024-01-01" },
                    { "GroupId", "9998" },
                    { "Quota", "1" },
                    { "CustomerName", "Test Customer" },
                    { "PvId", pvId.ToString() },
                    { "PvName", pvName }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "TotalAmount", "TotalAmount" },
                { "SaleStartDate", "SaleStartDate" },
                { "GroupId", "GroupId" },
                { "Quota", "Quota" },
                { "CustomerName", "CustomerName" },
                { "PvId", "PvId" },
                { "PvName", "PvName" }
            };

            // Act
            var result = await service.ExecuteContractDashboardImportAsync(
                uploadId, 
                rows, 
                mappings, 
                skipMissingContractNumber: false, 
                allowAutoCreateGroups: true, 
                allowAutoCreatePVs: false);

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.CreatedPVs.Should().BeEmpty();

            // Verify PV was NOT created
            var newPv = await context.PVs.FirstOrDefaultAsync(p => p.Name == pvName);
            newPv.Should().BeNull();
        }
    }
}
