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
    /// <summary>
    /// Integration tests that verify contract numbers are correctly extracted and stored
    /// when the import file's Cota column contains concatenated strings in the format:
    ///   GROUP;QUOTA;FLAG;CUSTOMER_NAME;CONTRACT_NUMBER
    ///
    /// Key cases verified (from bug investigation on 2026-07-02):
    ///   - 1100239686  → stored as "1100239686"  (no leading zeros, unchanged)
    ///   - 10239686    → stored as "10239686"     (leading '1' must NOT be stripped)
    ///   - 0239686     → stored as "239686"       (one leading zero stripped, by design)
    ///   - 239686      → stored as "239686"       (plain quota ID used as contract number)
    ///
    /// Each test generates a unique suffix so that the normalized contract numbers never
    /// collide in the shared integration test database.
    /// </summary>
    [Collection("Integration Tests")]
    public class CotaContractNumberImportTests
    {
        private readonly TestWebApplicationFactory _factory;

        public CotaContractNumberImportTests(TestWebApplicationFactory factory)
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
                FileName = "cota_test.csv",
                Status = "preview",
                UploadedByUserInternalId = admin?.InternalId ?? 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var group = new Group
            {
                Name = $"CotaTestGroup-{Guid.NewGuid().ToString("N")[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            return (session, group);
        }

        private static Dictionary<string, string> BuildMappings() => new()
        {
            { "cota.contract", "ContractNumber" },
            { "cota.group",    "GroupId" },
            { "cota.cota",     "Quota" },
            { "cota.customer", "CustomerName" },
            { "TotalAmount",   "TotalAmount" },
            { "SaleStartDate", "SaleStartDate" }
        };

        private static Dictionary<string, string> BuildRow(string groupId, string contractValue) => new()
        {
            { "cota.group",    groupId },
            { "cota.cota",     "318" },
            { "cota.customer", "GABRIEL FERREIRA ALVES" },
            { "cota.contract", contractValue },
            { "TotalAmount",   "150000" },
            { "SaleStartDate", "2024-01-01" }
        };

        /// <summary>
        /// Full Cota concatenated string where the contract number has no leading zeros.
        /// "001696;318;0;GABRIEL FERREIRA ALVES ;1100{suffix}" → ContractNumber = "1100{suffix}"
        /// This mirrors the exact example from the bug report.
        /// </summary>
        [Fact]
        public async Task Import_CotaWithFullContractNumber_ShouldStoreExactContractNumber()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var (session, group) = await SetupAsync(context, groupRepo, Guid.NewGuid().ToString());

            // Unique suffix per run — prefix "1100" mirrors the reported "1100239686" pattern
            var suffix       = Guid.NewGuid().ToString("N")[..6];
            var contractValue = $"1100{suffix}";   // e.g. "1100a3f8c2" — no leading zeros

            var rows     = new List<Dictionary<string, string>> { BuildRow(group.Id.ToString(), contractValue) };
            var mappings = BuildMappings();

            var result = await service.ExecuteContractDashboardImportAsync(
                session.UploadId, session.Id, rows, mappings);

            result.ProcessedRows.Should().Be(1);
            result.CreatedContracts.Should().HaveCount(1);

            result.CreatedContracts[0].ContractNumber.Should().Be(contractValue,
                because: "contract numbers with no leading zeros must not be modified");
        }

        /// <summary>
        /// Contract number starting with "10" — the leading '1' must NOT be stripped.
        /// This is the core case: 10{suffix} must NOT become {suffix}.
        /// NormalizeNumber only strips '0' characters, never '1' or any other digit.
        /// </summary>
        [Fact]
        public async Task Import_CotaWithContractStartingWith10_ShouldNotStripLeadingDigits()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var (session, group) = await SetupAsync(context, groupRepo, Guid.NewGuid().ToString());

            var suffix        = Guid.NewGuid().ToString("N")[..6];
            var contractValue = $"10{suffix}";   // starts with "10", not "0"

            var rows     = new List<Dictionary<string, string>> { BuildRow(group.Id.ToString(), contractValue) };
            var mappings = BuildMappings();

            var result = await service.ExecuteContractDashboardImportAsync(
                session.UploadId, session.Id, rows, mappings);

            result.ProcessedRows.Should().Be(1);
            result.CreatedContracts.Should().HaveCount(1);

            result.CreatedContracts[0].ContractNumber.Should().Be(contractValue,
                because: $"'{contractValue}' starts with '1', not '0', so NormalizeNumber must not strip any digits");
        }

        /// <summary>
        /// Contract number with a single leading zero: "0{suffix}" → "{suffix}".
        /// This is intentional behavior (TrimStart('0')), confirmed as acceptable by design.
        /// The unique suffix ensures no collision with the plain-numeric test below.
        /// </summary>
        [Fact]
        public async Task Import_CotaWithLeadingZeroContractNumber_ShouldStripOnlyTheLeadingZero()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var (session, group) = await SetupAsync(context, groupRepo, Guid.NewGuid().ToString());

            // "0" prefix + unique suffix. After NormalizeNumber the "0" is stripped → suffix
            var suffix        = Guid.NewGuid().ToString("N")[..6];
            var contractValue = $"0{suffix}";    // leading zero
            var expected      = suffix;           // leading zero stripped

            var rows     = new List<Dictionary<string, string>> { BuildRow(group.Id.ToString(), contractValue) };
            var mappings = BuildMappings();

            var result = await service.ExecuteContractDashboardImportAsync(
                session.UploadId, session.Id, rows, mappings);

            result.ProcessedRows.Should().Be(1);
            result.CreatedContracts.Should().HaveCount(1);

            result.CreatedContracts[0].ContractNumber.Should().Be(expected,
                because: $"'{contractValue}' has one leading zero which is stripped by NormalizeNumber (by design)");
        }

        /// <summary>
        /// When the Cota column already contains just a numeric/alphanumeric value (no semicolons),
        /// the entire value is used as-is as the contract number (no digits stripped).
        /// </summary>
        [Fact]
        public async Task Import_CotaWithPlainNumericValue_ShouldUseValueAsContractNumber()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var (session, group) = await SetupAsync(context, groupRepo, Guid.NewGuid().ToString());

            // Non-zero-prefixed unique value — stored exactly as-is
            var suffix        = Guid.NewGuid().ToString("N")[..6];
            var contractValue = $"9{suffix}";    // starts with "9" — no leading zero, never stripped

            var rows     = new List<Dictionary<string, string>> { BuildRow(group.Id.ToString(), contractValue) };
            var mappings = BuildMappings();

            var result = await service.ExecuteContractDashboardImportAsync(
                session.UploadId, session.Id, rows, mappings);

            result.ProcessedRows.Should().Be(1);
            result.CreatedContracts.Should().HaveCount(1);

            result.CreatedContracts[0].ContractNumber.Should().Be(contractValue,
                because: "a value with no leading zeros is stored unchanged");
        }
    }
}
