using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeScrapeConfigStoreNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            migrationBuilder.Sql(@"
CREATE TABLE ""ScrapeConfigs_nullstore_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ScrapeConfigs"" PRIMARY KEY AUTOINCREMENT,
    ""UserInternalId"" INTEGER NULL,
    ""Store"" TEXT NULL,
    ""Matricula"" TEXT NOT NULL,
    ""PowerBiPassword"" TEXT NULL,
    ""CredentialStatus"" TEXT NULL,
    ""DefaultStartMonth"" TEXT NULL,
    ""IsEnabled"" INTEGER NOT NULL DEFAULT 1,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ScrapeConfigs_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE CASCADE
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ScrapeConfigs_nullstore_tmp"" (
    ""Id"", ""UserInternalId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""DefaultStartMonth"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    ""Id"", ""UserInternalId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""DefaultStartMonth"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
FROM ""ScrapeConfigs"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ScrapeConfigs\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ScrapeConfigs_nullstore_tmp\" RENAME TO \"ScrapeConfigs\";", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_ScrapeConfigs_UserInternalId\" ON \"ScrapeConfigs\" (\"UserInternalId\");", suppressTransaction: true);
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
