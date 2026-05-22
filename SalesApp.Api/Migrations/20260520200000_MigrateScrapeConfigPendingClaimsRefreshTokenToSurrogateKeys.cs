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
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""RefreshTokens_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""Token"", ""ExpiresAt"", ""CreatedAt"", ""IsRevoked"", ""RevokedAt""
)
SELECT 
    rt.""Id"", u.""InternalId"", rt.""Token"", rt.""ExpiresAt"", rt.""CreatedAt"", rt.""IsRevoked"", rt.""RevokedAt""
FROM ""RefreshTokens"" rt
JOIN ""Users"" u ON rt.""UserId"" = u.""Id"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"RefreshTokens\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"RefreshTokens_dg_tmp\" RENAME TO \"RefreshTokens\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_Token\" ON \"RefreshTokens\" (\"Token\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_UserInternalId_IsRevoked\" ON \"RefreshTokens\" (\"UserInternalId\", \"IsRevoked\");", suppressTransaction: true);


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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ScrapeConfigs_dg_tmp"" (
    ""Id"", ""UserInternalId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    sc.""Id"", u.""InternalId"", sc.""Store"", sc.""Matricula"", sc.""PowerBiPassword"", sc.""CredentialStatus"", sc.""IsEnabled"", sc.""CreatedAt"", sc.""UpdatedAt""
FROM ""ScrapeConfigs"" sc
LEFT JOIN ""Users"" u ON sc.""UserId"" = u.""Id"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ScrapeConfigs\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ScrapeConfigs_dg_tmp\" RENAME TO \"ScrapeConfigs\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE INDEX \"IX_ScrapeConfigs_UserInternalId\" ON \"ScrapeConfigs\" (\"UserInternalId\");", suppressTransaction: true);


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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""PendingContractClaims_dg_tmp"" (
    ""Id"", ""ContractNumber"", ""UserInternalId"", ""MatriculaId"", ""ClaimedAt"", ""IsResolved"", ""ResolvedAt""
)
SELECT 
    pc.""Id"", pc.""ContractNumber"", u.""InternalId"", pc.""MatriculaId"", pc.""ClaimedAt"", pc.""IsResolved"", pc.""ResolvedAt""
FROM ""PendingContractClaims"" pc
JOIN ""Users"" u ON pc.""UserId"" = u.""Id"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"PendingContractClaims\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"PendingContractClaims_dg_tmp\" RENAME TO \"PendingContractClaims\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_PendingContractClaims_ContractNumber\" ON \"PendingContractClaims\" (\"ContractNumber\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_IsResolved\" ON \"PendingContractClaims\" (\"IsResolved\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_UserInternalId\" ON \"PendingContractClaims\" (\"UserInternalId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_MatriculaId\" ON \"PendingContractClaims\" (\"MatriculaId\");", suppressTransaction: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""RefreshTokens_dg_tmp"" (
    ""Id"", ""UserId"", ""Token"", ""ExpiresAt"", ""CreatedAt"", ""IsRevoked"", ""RevokedAt""
)
SELECT 
    rt.""Id"", u.""Id"", rt.""Token"", rt.""ExpiresAt"", rt.""CreatedAt"", rt.""IsRevoked"", rt.""RevokedAt""
FROM ""RefreshTokens"" rt
JOIN ""Users"" u ON rt.""UserInternalId"" = u.""InternalId"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"RefreshTokens\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"RefreshTokens_dg_tmp\" RENAME TO \"RefreshTokens\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_Token\" ON \"RefreshTokens\" (\"Token\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_RefreshTokens_UserId_IsRevoked\" ON \"RefreshTokens\" (\"UserId\", \"IsRevoked\");", suppressTransaction: true);


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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""ScrapeConfigs_dg_tmp"" (
    ""Id"", ""UserId"", ""Store"", ""Matricula"", ""PowerBiPassword"", ""CredentialStatus"", ""IsEnabled"", ""CreatedAt"", ""UpdatedAt""
)
SELECT 
    sc.""Id"", u.""Id"", sc.""Store"", sc.""Matricula"", sc.""PowerBiPassword"", sc.""CredentialStatus"", sc.""IsEnabled"", sc.""CreatedAt"", sc.""UpdatedAt""
FROM ""ScrapeConfigs"" sc
LEFT JOIN ""Users"" u ON sc.""UserInternalId"" = u.""InternalId"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"ScrapeConfigs\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"ScrapeConfigs_dg_tmp\" RENAME TO \"ScrapeConfigs\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE INDEX \"IX_ScrapeConfigs_UserId\" ON \"ScrapeConfigs\" (\"UserId\");", suppressTransaction: true);


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
);", suppressTransaction: true);

            migrationBuilder.Sql(@"
INSERT INTO ""PendingContractClaims_dg_tmp"" (
    ""Id"", ""UserId"", ""MatriculaId"", ""ClaimedAt"", ""IsResolved"", ""ResolvedAt""
)
SELECT 
    pc.""Id"", u.""Id"", pc.""MatriculaId"", pc.""ClaimedAt"", pc.""IsResolved"", pc.""ResolvedAt""
FROM ""PendingContractClaims"" pc
JOIN ""Users"" u ON pc.""UserInternalId"" = u.""InternalId"";", suppressTransaction: true);

            migrationBuilder.Sql("DROP TABLE \"PendingContractClaims\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"PendingContractClaims_dg_tmp\" RENAME TO \"PendingContractClaims\";", suppressTransaction: true);

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_PendingContractClaims_ContractNumber\" ON \"PendingContractClaims\" (\"ContractNumber\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_IsResolved\" ON \"PendingContractClaims\" (\"IsResolved\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_UserId\" ON \"PendingContractClaims\" (\"UserId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_PendingContractClaims_MatriculaId\" ON \"PendingContractClaims\" (\"MatriculaId\");", suppressTransaction: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
