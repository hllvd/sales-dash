using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateUserMatriculaImportSessionAuditLogToSurrogateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // --- 1. Rebuild UserMatriculas ---
            migrationBuilder.Sql(@"
CREATE TABLE ""UserMatriculas_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_UserMatriculas"" PRIMARY KEY AUTOINCREMENT,
    ""UserInternalId"" INTEGER NOT NULL,
    ""MatriculaId"" INTEGER NOT NULL,
    ""EndDate"" TEXT NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""IsOwner"" INTEGER NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""ImportSessionId"" INTEGER NULL,
    CONSTRAINT ""FK_UserMatriculas_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_UserMatriculas_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""UserMatriculas_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""MatriculaId"", ""EndDate"", ""IsActive"", ""IsOwner"", ""CreatedAt"", ""UpdatedAt"", ""ImportSessionId""
)
SELECT 
    um.""Id"", u.""InternalId"", um.""MatriculaId"", um.""EndDate"", um.""IsActive"", um.""IsOwner"", um.""CreatedAt"", um.""UpdatedAt"", um.""ImportSessionId""
FROM ""UserMatriculas"" um
JOIN ""Users"" u ON um.""UserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"UserMatriculas\";");
            migrationBuilder.Sql("ALTER TABLE \"UserMatriculas_dg_tmp\" RENAME TO \"UserMatriculas\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_UserMatriculas_UserInternalId_MatriculaId\" ON \"UserMatriculas\" (\"UserInternalId\", \"MatriculaId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_UserMatriculas_MatriculaId\" ON \"UserMatriculas\" (\"MatriculaId\") WHERE [IsOwner] = 1;");
            migrationBuilder.Sql("CREATE INDEX \"IX_UserMatriculas_UserInternalId\" ON \"UserMatriculas\" (\"UserInternalId\");");


            // --- 2. Rebuild ImportSessions ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportSessions_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportSessions"" PRIMARY KEY AUTOINCREMENT,
    ""CompletedAt"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""FailedRows"" INTEGER NOT NULL,
    ""FileName"" TEXT NOT NULL,
    ""FileType"" TEXT NOT NULL,
    ""Mappings"" TEXT NULL,
    ""ProcessedRows"" INTEGER NOT NULL,
    ""Status"" TEXT NOT NULL DEFAULT 'preview',
    ""TemplateId"" INTEGER NULL,
    ""TotalRows"" INTEGER NOT NULL,
    ""UploadId"" TEXT NOT NULL,
    ""UploadedByUserInternalId"" INTEGER NOT NULL,
    CONSTRAINT ""FK_ImportSessions_ImportTemplates_TemplateId"" FOREIGN KEY (""TemplateId"") REFERENCES ""ImportTemplates"" (""Id"") ON DELETE RESTRICT
);");

            migrationBuilder.Sql(@"
INSERT INTO ""ImportSessions_dg_tmp"" (
    ""Id"", ""CompletedAt"", ""CreatedAt"", ""FailedRows"", ""FileName"", ""FileType"", ""Mappings"", 
    ""ProcessedRows"", ""Status"", ""TemplateId"", ""TotalRows"", ""UploadId"", ""UploadedByUserInternalId""
)
SELECT 
    ims.""Id"", ims.""CompletedAt"", ims.""CreatedAt"", ims.""FailedRows"", ims.""FileName"", ims.""FileType"", ims.""Mappings"", 
    ims.""ProcessedRows"", ims.""Status"", ims.""TemplateId"", ims.""TotalRows"", ims.""UploadId"", u.""InternalId""
FROM ""ImportSessions"" ims
JOIN ""Users"" u ON ims.""UploadedByUserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"ImportSessions\";");
            migrationBuilder.Sql("ALTER TABLE \"ImportSessions_dg_tmp\" RENAME TO \"ImportSessions\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_ImportSessions_TemplateId\" ON \"ImportSessions\" (\"TemplateId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportSessions_UploadedByUserInternalId\" ON \"ImportSessions\" (\"UploadedByUserInternalId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_ImportSessions_UploadId\" ON \"ImportSessions\" (\"UploadId\");");


            // --- 3. Rebuild AuditLogs ---
            migrationBuilder.Sql(@"
CREATE TABLE ""AuditLogs_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY AUTOINCREMENT,
    ""UserInternalId"" INTEGER NOT NULL,
    ""Action"" TEXT NOT NULL,
    ""EntityName"" TEXT NOT NULL,
    ""EntityId"" TEXT NOT NULL,
    ""Changes"" TEXT NULL,
    ""Timestamp"" TEXT NOT NULL
);");

            migrationBuilder.Sql(@"
INSERT INTO ""AuditLogs_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""Action"", ""EntityName"", ""EntityId"", ""Changes"", ""Timestamp""
)
SELECT 
    al.""Id"", u.""InternalId"", al.""Action"", al.""EntityName"", al.""EntityId"", al.""Changes"", al.""Timestamp""
FROM ""AuditLogs"" al
JOIN ""Users"" u ON al.""UserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"AuditLogs\";");
            migrationBuilder.Sql("ALTER TABLE \"AuditLogs_dg_tmp\" RENAME TO \"AuditLogs\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_AuditLogs_UserInternalId\" ON \"AuditLogs\" (\"UserInternalId\");");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // --- 1. Rebuild UserMatriculas back to original schema ---
            migrationBuilder.Sql(@"
CREATE TABLE ""UserMatriculas_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_UserMatriculas"" PRIMARY KEY AUTOINCREMENT,
    ""UserId"" TEXT NOT NULL,
    ""MatriculaId"" INTEGER NOT NULL,
    ""EndDate"" TEXT NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""IsOwner"" INTEGER NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""ImportSessionId"" INTEGER NULL,
    CONSTRAINT ""FK_UserMatriculas_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_UserMatriculas_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_UserMatriculas_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""UserMatriculas_dg_tmp"" (
    ""Id"", ""UserId"", ""MatriculaId"", ""EndDate"", ""IsActive"", ""IsOwner"", ""CreatedAt"", ""UpdatedAt"", ""ImportSessionId""
)
SELECT 
    um.""Id"", u.""Id"", um.""MatriculaId"", um.""EndDate"", um.""IsActive"", um.""IsOwner"", um.""CreatedAt"", um.""UpdatedAt"", um.""ImportSessionId""
FROM ""UserMatriculas"" um
JOIN ""Users"" u ON um.""UserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"UserMatriculas\";");
            migrationBuilder.Sql("ALTER TABLE \"UserMatriculas_dg_tmp\" RENAME TO \"UserMatriculas\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_UserMatriculas_UserId_MatriculaId\" ON \"UserMatriculas\" (\"UserId\", \"MatriculaId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_UserMatriculas_MatriculaId\" ON \"UserMatriculas\" (\"MatriculaId\") WHERE [IsOwner] = 1;");
            migrationBuilder.Sql("CREATE INDEX \"IX_UserMatriculas_UserId\" ON \"UserMatriculas\" (\"UserId\");");


            // --- 2. Rebuild ImportSessions back to original schema ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ImportSessions_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ImportSessions"" PRIMARY KEY AUTOINCREMENT,
    ""CompletedAt"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""FailedRows"" INTEGER NOT NULL,
    ""FileName"" TEXT NOT NULL,
    ""FileType"" TEXT NOT NULL,
    ""Mappings"" TEXT NULL,
    ""ProcessedRows"" INTEGER NOT NULL,
    ""Status"" TEXT NOT NULL DEFAULT 'preview',
    ""TemplateId"" INTEGER NULL,
    ""TotalRows"" INTEGER NOT NULL,
    ""UploadId"" TEXT NOT NULL,
    ""UploadedByUserId"" TEXT NOT NULL,
    CONSTRAINT ""FK_ImportSessions_ImportTemplates_TemplateId"" FOREIGN KEY (""TemplateId"") REFERENCES ""ImportTemplates"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_ImportSessions_Users_UploadedByUserId"" FOREIGN KEY (""UploadedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);");

            migrationBuilder.Sql(@"
INSERT INTO ""ImportSessions_dg_tmp"" (
    ""Id"", ""CompletedAt"", ""CreatedAt"", ""FailedRows"", ""FileName"", ""FileType"", ""Mappings"", 
    ""ProcessedRows"", ""Status"", ""TemplateId"", ""TotalRows"", ""UploadId"", ""UploadedByUserId""
)
SELECT 
    ims.""Id"", ims.""CompletedAt"", ims.""CreatedAt"", ims.""FailedRows"", ims.""FileName"", ims.""FileType"", ims.""Mappings"", 
    ims.""ProcessedRows"", ims.""Status"", ims.""TemplateId"", ims.""TotalRows"", ims.""UploadId"", u.""Id""
FROM ""ImportSessions"" ims
JOIN ""Users"" u ON ims.""UploadedByUserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"ImportSessions\";");
            migrationBuilder.Sql("ALTER TABLE \"ImportSessions_dg_tmp\" RENAME TO \"ImportSessions\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_ImportSessions_TemplateId\" ON \"ImportSessions\" (\"TemplateId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_ImportSessions_UploadedByUserId\" ON \"ImportSessions\" (\"UploadedByUserId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_ImportSessions_UploadId\" ON \"ImportSessions\" (\"UploadId\");");


            // --- 3. Rebuild AuditLogs back to original schema ---
            migrationBuilder.Sql(@"
CREATE TABLE ""AuditLogs_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY AUTOINCREMENT,
    ""UserId"" TEXT NOT NULL,
    ""Action"" TEXT NOT NULL,
    ""EntityName"" TEXT NOT NULL,
    ""EntityId"" TEXT NOT NULL,
    ""Changes"" TEXT NULL,
    ""Timestamp"" TEXT NOT NULL,
    CONSTRAINT ""FK_AuditLogs_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);");

            migrationBuilder.Sql(@"
INSERT INTO ""AuditLogs_dg_tmp"" (
    ""Id"", ""UserId"", ""Action"", ""EntityName"", ""EntityId"", ""Changes"", ""Timestamp""
)
SELECT 
    al.""Id"", u.""Id"", al.""Action"", al.""EntityName"", al.""EntityId"", al.""Changes"", al.""Timestamp""
FROM ""AuditLogs"" al
JOIN ""Users"" u ON al.""UserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"AuditLogs\";");
            migrationBuilder.Sql("ALTER TABLE \"AuditLogs_dg_tmp\" RENAME TO \"AuditLogs\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_AuditLogs_UserId\" ON \"AuditLogs\" (\"UserId\");");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }
    }
}
