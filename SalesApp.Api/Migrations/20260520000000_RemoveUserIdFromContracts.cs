using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserIdFromContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // We use PRAGMA foreign_keys = OFF to allow rebuilding the table
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // 1. Create a temporary table with the new schema (without the UserId column and its foreign key)
            migrationBuilder.Sql(@"
CREATE TABLE ""Contracts_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Contracts"" PRIMARY KEY AUTOINCREMENT,
    ""CategoryMetadataId"" INTEGER NULL,
    ""ContractNumber"" TEXT NOT NULL,
    ""ContractStatusId"" INTEGER NOT NULL,
    ""ContractType"" INTEGER NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""CustomerName"" TEXT NULL,
    ""GroupId"" INTEGER NULL,
    ""ImportSessionId"" INTEGER NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""MatriculaId"" INTEGER NULL,
    ""PlanoVendaMetadataId"" INTEGER NULL,
    ""PvId"" INTEGER NULL,
    ""Quota"" INTEGER NULL,
    ""SaleStartDate"" TEXT NOT NULL,
    ""TempMatricula"" TEXT NULL,
    ""TotalAmount"" decimal(18,2) NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""UploadId"" TEXT NULL,
    ""Version"" INTEGER NULL, 
    ""UserInternalId"" INTEGER NULL,
    CONSTRAINT ""FK_Contracts_ContractMetadata_CategoryMetadataId"" FOREIGN KEY (""CategoryMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractMetadata_PlanoVendaMetadataId"" FOREIGN KEY (""PlanoVendaMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractStatuses_ContractStatusId"" FOREIGN KEY (""ContractStatusId"") REFERENCES ""ContractStatuses"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Groups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_PVs_PvId"" FOREIGN KEY (""PvId"") REFERENCES ""PVs"" (""Id"") ON DELETE RESTRICT
);");

            // 2. Copy the existing data to the temporary table (UserId is dropped here)
            migrationBuilder.Sql(@"
INSERT INTO ""Contracts_dg_tmp"" (
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", ""ContractType"", 
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", ""Version"", ""UserInternalId""
)
SELECT 
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", ""ContractType"", 
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", ""Version"", ""UserInternalId""
FROM ""Contracts"";");

            // 3. Drop the old table
            migrationBuilder.Sql("DROP TABLE \"Contracts\";");

            // 4. Rename the temporary table to the original table name
            migrationBuilder.Sql("ALTER TABLE \"Contracts_dg_tmp\" RENAME TO \"Contracts\";");

            // 5. Recreate the standard indexes on the new table
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_CategoryMetadataId\" ON \"Contracts\" (\"CategoryMetadataId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Contracts_ContractNumber\" ON \"Contracts\" (\"ContractNumber\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ContractStatusId\" ON \"Contracts\" (\"ContractStatusId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_GroupId\" ON \"Contracts\" (\"GroupId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ImportSessionId\" ON \"Contracts\" (\"ImportSessionId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_MatriculaId\" ON \"Contracts\" (\"MatriculaId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PlanoVendaMetadataId\" ON \"Contracts\" (\"PlanoVendaMetadataId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PvId\" ON \"Contracts\" (\"PvId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_UserInternalId\" ON \"Contracts\" (\"UserInternalId\");");

            // Turn foreign key constraints back ON
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // 1. Create a temporary table with the old schema (re-adding the UserId column and its foreign key)
            migrationBuilder.Sql(@"
CREATE TABLE ""Contracts_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Contracts"" PRIMARY KEY AUTOINCREMENT,
    ""CategoryMetadataId"" INTEGER NULL,
    ""ContractNumber"" TEXT NOT NULL,
    ""ContractStatusId"" INTEGER NOT NULL,
    ""ContractType"" INTEGER NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""CustomerName"" TEXT NULL,
    ""GroupId"" INTEGER NULL,
    ""ImportSessionId"" INTEGER NULL,
    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
    ""MatriculaId"" INTEGER NULL,
    ""PlanoVendaMetadataId"" INTEGER NULL,
    ""PvId"" INTEGER NULL,
    ""Quota"" INTEGER NULL,
    ""SaleStartDate"" TEXT NOT NULL,
    ""TempMatricula"" TEXT NULL,
    ""TotalAmount"" decimal(18,2) NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""UploadId"" TEXT NULL,
    ""UserId"" TEXT NULL,
    ""Version"" INTEGER NULL, 
    ""UserInternalId"" INTEGER NULL,
    CONSTRAINT ""FK_Contracts_ContractMetadata_CategoryMetadataId"" FOREIGN KEY (""CategoryMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractMetadata_PlanoVendaMetadataId"" FOREIGN KEY (""PlanoVendaMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractStatuses_ContractStatusId"" FOREIGN KEY (""ContractStatusId"") REFERENCES ""ContractStatuses"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Groups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_PVs_PvId"" FOREIGN KEY (""PvId"") REFERENCES ""PVs"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);");

            // 2. Copy the existing data to the temporary table (UserId is set to NULL because we don't have it anymore)
            migrationBuilder.Sql(@"
INSERT INTO ""Contracts_dg_tmp"" (
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", ""ContractType"", 
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", ""UserId"", ""Version"", ""UserInternalId""
)
SELECT 
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", ""ContractType"", 
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", NULL, ""Version"", ""UserInternalId""
FROM ""Contracts"";");

            // 3. Drop the new table
            migrationBuilder.Sql("DROP TABLE \"Contracts\";");

            // 4. Rename the temporary table to the original table name
            migrationBuilder.Sql("ALTER TABLE \"Contracts_dg_tmp\" RENAME TO \"Contracts\";");

            // 5. Recreate all indexes including the UserId index
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_CategoryMetadataId\" ON \"Contracts\" (\"CategoryMetadataId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Contracts_ContractNumber\" ON \"Contracts\" (\"ContractNumber\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ContractStatusId\" ON \"Contracts\" (\"ContractStatusId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_GroupId\" ON \"Contracts\" (\"GroupId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ImportSessionId\" ON \"Contracts\" (\"ImportSessionId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_MatriculaId\" ON \"Contracts\" (\"MatriculaId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PlanoVendaMetadataId\" ON \"Contracts\" (\"PlanoVendaMetadataId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PvId\" ON \"Contracts\" (\"PvId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_UserId\" ON \"Contracts\" (\"UserId\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_UserInternalId\" ON \"Contracts\" (\"UserInternalId\");");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }
    }
}
