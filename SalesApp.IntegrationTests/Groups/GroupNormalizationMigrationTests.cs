using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Groups
{
    [Collection("Misc Tests")]
    public class GroupNormalizationMigrationTests
    {
        private readonly MiscTestFactory _factory;

        public GroupNormalizationMigrationTests(MiscTestFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GroupNormalization_ShouldConsolidateDuplicatesAndMigrateContracts()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Unique suffix to avoid clashes with seeded data
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var canonicalName = $"560{suffix}";
            var zeroPaddedName = $"000{canonicalName}";
            var soloPaddedCanonical = $"999{suffix}";
            var soloPaddedName = $"000{soloPaddedCanonical}";
            var normalName = $"1020{suffix}";

            // 1. Arrange Groups
            var zeroGroup = new Group
            {
                Name = zeroPaddedName,
                Description = "Auto-created 2026-04-29",
                Commission = 10,
                Segment = (int)Segment.Lar,
                IsActive = true
            };
            var canonicalGroup = new Group
            {
                Name = canonicalName,
                Description = "Auto-created 2026-06-18",
                Commission = 10,
                Segment = null, // Will inherit from zeroGroup
                IsActive = true
            };
            var soloZeroGroup = new Group
            {
                Name = soloPaddedName,
                Description = "Auto-created 2026-04-29",
                Commission = 5,
                Segment = (int)Segment.Motores,
                IsActive = true
            };
            var normalGroup = new Group
            {
                Name = normalName,
                Description = "Normal",
                Commission = 0,
                Segment = (int)Segment.Servicos,
                IsActive = true
            };

            db.Groups.AddRange(zeroGroup, canonicalGroup, soloZeroGroup, normalGroup);
            await db.SaveChangesAsync();

            // 2. Arrange Contracts
            var c1 = new Contract
            {
                ContractNumber = $"CNT-1-{suffix}",
                GroupId = zeroGroup.Id,
                ContractStatusId = 1,
                SaleStartDate = DateTime.UtcNow,
                TotalAmount = 1000
            };
            var c2 = new Contract
            {
                ContractNumber = $"CNT-2-{suffix}",
                GroupId = canonicalGroup.Id,
                ContractStatusId = 1,
                SaleStartDate = DateTime.UtcNow,
                TotalAmount = 2000
            };
            var c3 = new Contract
            {
                ContractNumber = $"CNT-3-{suffix}",
                GroupId = soloZeroGroup.Id,
                ContractStatusId = 1,
                SaleStartDate = DateTime.UtcNow,
                TotalAmount = 3000
            };
            var c4 = new Contract
            {
                ContractNumber = $"CNT-4-{suffix}",
                GroupId = normalGroup.Id,
                ContractStatusId = 1,
                SaleStartDate = DateTime.UtcNow,
                TotalAmount = 4000
            };

            db.Contracts.AddRange(c1, c2, c3, c4);
            await db.SaveChangesAsync();

            // 3. Act: Execute the exact migration SQL steps
            await db.Database.ExecuteSqlRawAsync(@"
                DROP TABLE IF EXISTS GroupMappings;
                CREATE TEMP TABLE GroupMappings AS
                SELECT 
                    g.Id AS OldGroupId,
                    g.Name AS OldName,
                    (CASE WHEN LTRIM(g.Name, '0') = '' THEN '0' ELSE LTRIM(g.Name, '0') END) AS NormalizedName,
                    COALESCE(
                        exact_match.Id,
                        min_match.MinId
                    ) AS CanonicalGroupId
                FROM Groups g
                LEFT JOIN Groups exact_match 
                    ON exact_match.Name = (CASE WHEN LTRIM(g.Name, '0') = '' THEN '0' ELSE LTRIM(g.Name, '0') END)
                LEFT JOIN (
                    SELECT 
                        (CASE WHEN LTRIM(Name, '0') = '' THEN '0' ELSE LTRIM(Name, '0') END) AS NormName,
                        MIN(Id) AS MinId
                    FROM Groups
                    GROUP BY (CASE WHEN LTRIM(Name, '0') = '' THEN '0' ELSE LTRIM(Name, '0') END)
                ) min_match 
                    ON min_match.NormName = (CASE WHEN LTRIM(g.Name, '0') = '' THEN '0' ELSE LTRIM(g.Name, '0') END);
            ");

            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE Groups
                SET Segment = (
                    SELECT m_old_grp.Segment
                    FROM GroupMappings m
                    JOIN Groups m_old_grp ON m_old_grp.Id = m.OldGroupId
                    WHERE m.CanonicalGroupId = Groups.Id
                      AND m_old_grp.Segment IS NOT NULL
                    ORDER BY m_old_grp.Id DESC
                    LIMIT 1
                )
                WHERE Segment IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM GroupMappings m
                    JOIN Groups m_old_grp ON m_old_grp.Id = m.OldGroupId
                    WHERE m.CanonicalGroupId = Groups.Id
                      AND m_old_grp.Segment IS NOT NULL
                  );
            ");

            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE Contracts
                SET GroupId = (
                    SELECT m.CanonicalGroupId
                    FROM GroupMappings m
                    WHERE m.OldGroupId = Contracts.GroupId
                )
                WHERE GroupId IN (
                    SELECT OldGroupId FROM GroupMappings WHERE OldGroupId != CanonicalGroupId
                );
            ");

            await db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM Groups
                WHERE Id IN (
                    SELECT OldGroupId FROM GroupMappings WHERE OldGroupId != CanonicalGroupId
                );
            ");

            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE Groups
                SET Name = (
                    SELECT m.NormalizedName
                    FROM GroupMappings m
                    WHERE m.CanonicalGroupId = Groups.Id
                    LIMIT 1
                )
                WHERE Id IN (
                    SELECT CanonicalGroupId FROM GroupMappings
                )
                AND Name != (CASE WHEN LTRIM(Name, '0') = '' THEN '0' ELSE LTRIM(Name, '0') END);
            ");

            await db.Database.ExecuteSqlRawAsync(@"
                DROP TABLE IF EXISTS GroupMappings;
            ");

            // 4. Assert: Verify database state
            // Detach local tracker cache to inspect actual DB rows
            db.ChangeTracker.Clear();

            // A. zeroGroup (000560) should be deleted
            var checkZeroGroup = await db.Groups.FindAsync(zeroGroup.Id);
            checkZeroGroup.Should().BeNull();

            // B. canonicalGroup (560) should remain, and its Segment must be inherited (Lar = 1)
            var checkCanonicalGroup = await db.Groups.FindAsync(canonicalGroup.Id);
            checkCanonicalGroup.Should().NotBeNull();
            checkCanonicalGroup!.Name.Should().Be(canonicalName);
            checkCanonicalGroup.Segment.Should().Be((int)Segment.Lar);

            // C. soloZeroGroup (000999) should be renamed to 999, keeping same ID and segment
            var checkSoloGroup = await db.Groups.FindAsync(soloZeroGroup.Id);
            checkSoloGroup.Should().NotBeNull();
            checkSoloGroup!.Name.Should().Be(soloPaddedCanonical);
            checkSoloGroup.Segment.Should().Be((int)Segment.Motores);

            // D. normalGroup should be untouched
            var checkNormalGroup = await db.Groups.FindAsync(normalGroup.Id);
            checkNormalGroup.Should().NotBeNull();
            checkNormalGroup!.Name.Should().Be(normalName);
            checkNormalGroup.Segment.Should().Be((int)Segment.Servicos);

            // E. Contracts check
            var checkC1 = await db.Contracts.FindAsync(c1.Id);
            checkC1!.GroupId.Should().Be(canonicalGroup.Id, "Contract 1 should have been migrated to the canonical group");

            var checkC2 = await db.Contracts.FindAsync(c2.Id);
            checkC2!.GroupId.Should().Be(canonicalGroup.Id, "Contract 2 was already pointing to the canonical group");

            var checkC3 = await db.Contracts.FindAsync(c3.Id);
            checkC3!.GroupId.Should().Be(soloZeroGroup.Id, "Contract 3 should still point to group 3 which was renamed in place");

            var checkC4 = await db.Contracts.FindAsync(c4.Id);
            checkC4!.GroupId.Should().Be(normalGroup.Id);
        }
    }
}
