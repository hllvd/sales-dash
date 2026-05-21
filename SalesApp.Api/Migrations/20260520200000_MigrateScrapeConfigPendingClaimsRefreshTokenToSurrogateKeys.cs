using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateScrapeConfigPendingClaimsRefreshTokenToSurrogateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // --- 1. Rebuild RefreshTokens ---
            migrationBuilder.Sql(@"
CREATE TABLE ""RefreshTokens_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_RefreshTokens"" PRIMARY KEY AUTOINCREMENT,
    ""UserInternalId"" INTEGER NOT NULL,
    ""Token"" TEXT NOT NULL,
    ""ExpiresAt"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""IsRevoked"" INTEGER NOT NULL DEFAULT 0,
    ""RevokedAt"" TEXT NULL,
    CONSTRAINT ""FK_RefreshTokens_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""RefreshTokens_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""Token"", ""ExpiresAt"", ""CreatedAt"", ""IsRevoked"", ""RevokedAt""
)
SELECT 
    rt.""Id"", u.""InternalId"", rt.""Token"", rt.""ExpiresAt"", rt.""CreatedAt"", rt.""IsRevoked"", rt.""RevokedAt""
FROM ""RefreshTokens"" rt
JOIN ""Users"" u ON rt.""UserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"RefreshTokens\";");
            migrationBuilder.Sql("ALTER TABLE \"RefreshTokens_dg_tmp\" RENAME TO \"RefreshTokens\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_Token\" ON \"RefreshTokens\" (\"Token\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_UserInternalId_IsRevoked\" ON \"RefreshTokens\" (\"UserInternalId\", \"IsRevoked\");");


            // --- 2. Rebuild ScrapeConfigs ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ScrapeConfigs_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ScrapeConfigs"" PRIMARY KEY AUTOINCREMENT,
    ""UserInternalId"" INTEGER NULL,
    ""Store"" TEXT NOT NULL,
    ""Matricula"" TEXT NOT NULL,
    ""PowerBiPassword"" TEXT NULL,
    ""CredentialStatus"" TEXT NULL,
    ""IsEnabled"" INTEGER NOT NULL DEFAULT 1,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ScrapeConfigs_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""ScrapeConfigs_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    sc.""Id"", u.""InternalId"", sc.""Store"", sc.""Matricula"", sc.""PowerBiPassword"", sc.""CredentialStatus"", sc.""IsEnabled"", sc.""CreatedAt"", sc.""UpdatedAt""
FROM ""ScrapeConfigs"" sc
LEFT JOIN ""Users"" u ON sc.""UserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"ScrapeConfigs\";");
            migrationBuilder.Sql("ALTER TABLE \"ScrapeConfigs_dg_tmp\" RENAME TO \"ScrapeConfigs\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_ScrapeConfigs_UserInternalId\" ON \"ScrapeConfigs\" (\"UserInternalId\");");


            // --- 3. Rebuild PendingContractClaims ---
            migrationBuilder.Sql(@"
CREATE TABLE ""PendingContractClaims_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PendingContractClaims"" PRIMARY KEY AUTOINCREMENT,
    ""ContractNumber"" TEXT NOT NULL,
    ""UserInternalId"" INTEGER NOT NULL,
    ""MatriculaId"" INTEGER NOT NULL,
    ""ClaimedAt"" TEXT NOT NULL,
    ""IsResolved"" INTEGER NOT NULL DEFAULT 0,
    ""ResolvedAt"" TEXT NULL,
    CONSTRAINT ""FK_PendingContractClaims_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE CASCADE,
    CONSTRAINT ""FK_PendingContractClaims_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT
);");

            migrationBuilder.Sql(@"
INSERT INTO ""PendingContractClaims_dg_tmp"" (
    ""Id"", ""ContractNumber"", ""UserInternalId"", ""MatriculaId"", ""ClaimedAt"", ""IsResolved"", ""ResolvedAt""
)
SELECT 
    pc.""Id"", pc.""ContractNumber"", u.""InternalId"", pc.""MatriculaId"", pc.""ClaimedAt"", pc.""IsResolved"", pc.""ResolvedAt""
FROM ""PendingContractClaims"" pc
JOIN ""Users"" u ON pc.""UserId"" = u.""Id"";");

            migrationBuilder.Sql("DROP TABLE \"PendingContractClaims\";");
            migrationBuilder.Sql("ALTER TABLE \"PendingContractClaims_dg_tmp\" RENAME TO \"PendingContractClaims\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_PendingContractClaims_ContractNumber\" ON \"PendingContractClaims\" (\"ContractNumber\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_IsResolved\" ON \"PendingContractClaims\" (\"IsResolved\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_UserInternalId\" ON \"PendingContractClaims\" (\"UserInternalId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_MatriculaId\" ON \"PendingContractClaims\" (\"MatriculaId\");");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // --- 1. Revert RefreshTokens ---
            migrationBuilder.Sql(@"
CREATE TABLE ""RefreshTokens_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_RefreshTokens"" PRIMARY KEY AUTOINCREMENT,
    ""UserId"" TEXT NOT NULL,
    ""Token"" TEXT NOT NULL,
    ""ExpiresAt"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""IsRevoked"" INTEGER NOT NULL DEFAULT 0,
    ""RevokedAt"" TEXT NULL,
    CONSTRAINT ""FK_RefreshTokens_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""RefreshTokens_dg_tmp"" (
    ""Id"", ""UserId"", ""Token"", ""ExpiresAt"", ""CreatedAt"", ""IsRevoked"", ""RevokedAt""
)
SELECT 
    rt.""Id"", u.""Id"", rt.""Token"", rt.""ExpiresAt"", rt.""CreatedAt"", rt.""IsRevoked"", rt.""RevokedAt""
FROM ""RefreshTokens"" rt
JOIN ""Users"" u ON rt.""UserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"RefreshTokens\";");
            migrationBuilder.Sql("ALTER TABLE \"RefreshTokens_dg_tmp\" RENAME TO \"RefreshTokens\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_Token\" ON \"RefreshTokens\" (\"Token\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_UserId_IsRevoked\" ON \"RefreshTokens\" (\"UserId\", \"IsRevoked\");");


            // --- 2. Revert ScrapeConfigs ---
            migrationBuilder.Sql(@"
CREATE TABLE ""ScrapeConfigs_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ScrapeConfigs"" PRIMARY KEY AUTOINCREMENT,
    ""UserId"" TEXT NULL,
    ""Store"" TEXT NOT NULL,
    ""Matricula"" TEXT NOT NULL,
    ""PowerBiPassword"" TEXT NULL,
    ""CredentialStatus"" TEXT NULL,
    ""IsEnabled"" INTEGER NOT NULL DEFAULT 1,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_ScrapeConfigs_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
INSERT INTO ""ScrapeConfigs_dg_tmp"" (
    ""Id"", ""UserId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    sc.""Id"", u.""Id"", sc.""Store"", sc.""Matricula"", sc.""PowerBiPassword"", sc.""CredentialStatus"", sc.""IsEnabled"", sc.""CreatedAt"", sc.""UpdatedAt""
FROM ""ScrapeConfigs"" sc
LEFT JOIN ""Users"" u ON sc.""UserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"ScrapeConfigs\";");
            migrationBuilder.Sql("ALTER TABLE \"ScrapeConfigs_dg_tmp\" RENAME TO \"ScrapeConfigs\";");

            migrationBuilder.Sql("CREATE INDEX \"IX_ScrapeConfigs_UserId\" ON \"ScrapeConfigs\" (\"UserId\");");


            // --- 3. Revert PendingContractClaims ---
            migrationBuilder.Sql(@"
CREATE TABLE ""PendingContractClaims_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PendingContractClaims"" PRIMARY KEY AUTOINCREMENT,
    ""ContractNumber"" TEXT NOT NULL,
    ""UserId"" TEXT NOT NULL,
    ""MatriculaId"" INTEGER NOT NULL,
    ""ClaimedAt"" TEXT NOT NULL,
    ""IsResolved"" INTEGER NOT NULL DEFAULT 0,
    ""ResolvedAt"" TEXT NULL,
    CONSTRAINT ""FK_PendingContractClaims_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_PendingContractClaims_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT
);");

            migrationBuilder.Sql(@"
INSERT INTO ""PendingContractClaims_dg_tmp"" (
    ""Id"", ""UserId"", ""MatriculaId"", ""ClaimedAt"", ""IsResolved"", ""ResolvedAt""
)
SELECT 
    pc.""Id"", u.""Id"", pc.""MatriculaId"", pc.""ClaimedAt"", pc.""IsResolved"", pc.""ResolvedAt""
FROM ""PendingContractClaims"" pc
JOIN ""Users"" u ON pc.""UserInternalId"" = u.""InternalId"";");

            migrationBuilder.Sql("DROP TABLE \"PendingContractClaims\";");
            migrationBuilder.Sql("ALTER TABLE \"PendingContractClaims_dg_tmp\" RENAME TO \"PendingContractClaims\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_PendingContractClaims_ContractNumber\" ON \"PendingContractClaims\" (\"ContractNumber\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_IsResolved\" ON \"PendingContractClaims\" (\"IsResolved\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_UserId\" ON \"PendingContractClaims\" (\"UserId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_MatriculaId\" ON \"PendingContractClaims\" (\"MatriculaId\");");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }
    }
}
