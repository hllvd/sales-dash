using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeGroupNamesAndDeduplicate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Build temporary mapping table
            migrationBuilder.Sql(@"
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

            // 2. Preserve Segment on canonical group if canonical Segment is NULL and any duplicate has a Segment
            migrationBuilder.Sql(@"
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

            // 3. Migrate Contracts pointing to duplicate groups to canonical group
            migrationBuilder.Sql(@"
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

            // 4. Delete old duplicate groups
            migrationBuilder.Sql(@"
                DELETE FROM Groups
                WHERE Id IN (
                    SELECT OldGroupId FROM GroupMappings WHERE OldGroupId != CanonicalGroupId
                );
            ");

            // 5. Rename canonical groups that still contain leading zeros to their normalized name
            migrationBuilder.Sql(@"
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

            // 6. Drop temporary mapping table
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS GroupMappings;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data consolidation cannot be automatically reversed.
        }
    }
}
