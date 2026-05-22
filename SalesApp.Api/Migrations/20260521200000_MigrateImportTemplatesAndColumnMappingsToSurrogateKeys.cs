using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateImportTemplatesAndColumnMappingsToSurrogateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            // --- 1. Rebuild ImportTemplates ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportTemplates_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportTemplates"" PRIMARY KEY AUTOINCREMENT,
    ""Name"" TEXT NOT NULL,
    ""EntityType"" TEXT NOT NULL,
    ""Description"" TEXT NOT NULL,
    ""RequiredFields"" TEXT NOT NULL,
    ""OptionalFields"" TEXT NOT NULL,
    ""DefaultMappings"" TEXT NOT NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""CreatedByUserInternalId"" INTEGER NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ImportTemplates_Users_CreatedByUserInternalId"" FOREIGN KEY (""CreatedByUserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE RESTRICT
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ImportTemplates_dg_tmp"" (
    ""Id"", ""Name"", ""EntityType"", ""Description"", ""RequiredFields"", ""OptionalFields"", ""DefaultMappings"", ""IsActive"", ""CreatedByUserInternalId"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    it.""Id"", it.""Name"", it.""EntityType"", it.""Description"", it.""RequiredFields"", it.""OptionalFields"", it.""DefaultMappings"", it.""IsActive"", u.""InternalId"", it.""CreatedAt"", it.""UpdatedAt""
FROM ""ImportTemplates"" it
JOIN ""Users"" u ON it.""CreatedByUserId"" = u.""Id"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ImportTemplates\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ImportTemplates_dg_tmp\" RENAME TO \"ImportTemplates\";", suppressTransaction: true);
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_ImportTemplates_Name\" ON \"ImportTemplates\" (\"Name\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportTemplates_CreatedByUserInternalId\" ON \"ImportTemplates\" (\"CreatedByUserInternalId\");", suppressTransaction: true);

            // --- 2. Rebuild ImportColumnMappings ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportColumnMappings_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportColumnMappings"" PRIMARY KEY AUTOINCREMENT,
    ""MappingName"" TEXT NOT NULL,
    ""FileType"" TEXT NOT NULL,
    ""SourceColumn"" TEXT NOT NULL,
    ""TargetField"" TEXT NOT NULL,
    ""IsRequired"" INTEGER NOT NULL,
    ""CreatedByUserInternalId"" INTEGER NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ImportColumnMappings_Users_CreatedByUserInternalId"" FOREIGN KEY (""CreatedByUserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE RESTRICT
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ImportColumnMappings_dg_tmp"" (
    ""Id"", ""MappingName"", ""FileType"", ""SourceColumn"", ""TargetField"", ""IsRequired"", ""CreatedByUserInternalId"", ""CreatedAt""
)
SELECT 
    icm.""Id"", icm.""MappingName"", icm.""FileType"", icm.""SourceColumn"", icm.""TargetField"", icm.""IsRequired"", u.""InternalId"", icm.""CreatedAt""
FROM ""ImportColumnMappings"" icm
JOIN ""Users"" u ON icm.""CreatedByUserId"" = u.""Id"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ImportColumnMappings\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ImportColumnMappings_dg_tmp\" RENAME TO \"ImportColumnMappings\";", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportColumnMappings_CreatedByUserInternalId\" ON \"ImportColumnMappings\" (\"CreatedByUserInternalId\");", suppressTransaction: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            // --- 1. Revert ImportTemplates ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportTemplates_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportTemplates"" PRIMARY KEY AUTOINCREMENT,
    ""Name"" TEXT NOT NULL,
    ""EntityType"" TEXT NOT NULL,
    ""Description"" TEXT NOT NULL,
    ""RequiredFields"" TEXT NOT NULL,
    ""OptionalFields"" TEXT NOT NULL,
    ""DefaultMappings"" TEXT NOT NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""CreatedByUserId"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ImportTemplates_Users_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ImportTemplates_dg_tmp"" (
    ""Id"", ""Name"", ""EntityType"", ""Description"", ""RequiredFields"", ""OptionalFields"", ""DefaultMappings"", ""IsActive"", ""CreatedByUserId"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    it.""Id"", it.""Name"", it.""EntityType"", it.""Description"", it.""RequiredFields"", it.""OptionalFields"", it.""DefaultMappings"", it.""IsActive"", u.""Id"", it.""CreatedAt"", it.""UpdatedAt""
FROM ""ImportTemplates"" it
JOIN ""Users"" u ON it.""CreatedByUserInternalId"" = u.""InternalId"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ImportTemplates\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ImportTemplates_dg_tmp\" RENAME TO \"ImportTemplates\";", suppressTransaction: true);
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_ImportTemplates_Name\" ON \"ImportTemplates\" (\"Name\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportTemplates_CreatedByUserId\" ON \"ImportTemplates\" (\"CreatedByUserId\");", suppressTransaction: true);

            // --- 2. Revert ImportColumnMappings ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportColumnMappings_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportColumnMappings"" PRIMARY KEY AUTOINCREMENT,
    ""MappingName"" TEXT NOT NULL,
    ""FileType"" TEXT NOT NULL,
    ""SourceColumn"" TEXT NOT NULL,
    ""TargetField"" TEXT NOT NULL,
    ""IsRequired"" INTEGER NOT NULL,
    ""CreatedByUserId"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ImportColumnMappings_Users_CreatedByUserId"" FOREIGN KEY (""CreatedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ImportColumnMappings_dg_tmp"" (
    ""Id"", ""MappingName"", ""FileType"", ""SourceColumn"", ""TargetField"", ""IsRequired"", ""CreatedByUserId"", ""CreatedAt""
)
SELECT 
    icm.""Id"", icm.""MappingName"", icm.""FileType"", icm.""SourceColumn"", icm.""TargetField"", icm.""IsRequired"", u.""Id"", icm.""CreatedAt""
FROM ""ImportColumnMappings"" icm
JOIN ""Users"" u ON icm.""CreatedByUserInternalId"" = u.""InternalId"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ImportColumnMappings\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ImportColumnMappings_dg_tmp\" RENAME TO \"ImportColumnMappings\";", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportColumnMappings_CreatedByUserId\" ON \"ImportColumnMappings\" (\"CreatedByUserId\");", suppressTransaction: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
