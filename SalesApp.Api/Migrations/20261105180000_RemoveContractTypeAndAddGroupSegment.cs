using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContractTypeAndAddGroupSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // We use PRAGMA foreign_keys = OFF to allow rebuilding the table
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            // 1. Add Segment column to Groups table
            migrationBuilder.Sql("ALTER TABLE \"Groups\" ADD COLUMN \"Segment\" INTEGER NULL;", suppressTransaction: true);

            // 2. Create temporary table without ContractType
            migrationBuilder.Sql(@"
CREATE TABLE ""Contracts_dg_tmp"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Contracts"" PRIMARY KEY AUTOINCREMENT,
    ""CategoryMetadataId"" INTEGER NULL,
    ""ContractNumber"" TEXT NOT NULL,
    ""ContractStatusId"" INTEGER NOT NULL,
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
    ""RawStatus"" TEXT NULL,
    ""TempMatricula"" TEXT NULL,
    ""TotalAmount"" decimal(18,2) NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""UploadId"" TEXT NULL,
    ""UserInternalId"" INTEGER NULL,
    ""Version"" INTEGER NULL,
    ""HasPayment"" INTEGER NULL,
    CONSTRAINT ""FK_Contracts_ContractMetadata_CategoryMetadataId"" FOREIGN KEY (""CategoryMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractMetadata_PlanoVendaMetadataId"" FOREIGN KEY (""PlanoVendaMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractStatuses_ContractStatusId"" FOREIGN KEY (""ContractStatusId"") REFERENCES ""ContractStatuses"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Groups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_PVs_PvId"" FOREIGN KEY (""PvId"") REFERENCES ""PVs"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE RESTRICT
);", suppressTransaction: true);

            // 3. Copy existing data excluding ContractType
            migrationBuilder.Sql(@"
INSERT INTO ""Contracts_dg_tmp"" (
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"",
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""RawStatus"", ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", 
    ""UserInternalId"", ""Version"", ""HasPayment""
)
SELECT 
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", 
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""RawStatus"", ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", 
    ""UserInternalId"", ""Version"", ""HasPayment""
FROM ""Contracts"";", suppressTransaction: true);

            // 4. Drop the old table
            migrationBuilder.Sql("DROP TABLE \"Contracts\";", suppressTransaction: true);

            // 5. Rename temporary table
            migrationBuilder.Sql("ALTER TABLE \"Contracts_dg_tmp\" RENAME TO \"Contracts\";", suppressTransaction: true);

            // 6. Recreate indexes
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_CategoryMetadataId\" ON \"Contracts\" (\"CategoryMetadataId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Contracts_ContractNumber\" ON \"Contracts\" (\"ContractNumber\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ContractStatusId\" ON \"Contracts\" (\"ContractStatusId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_GroupId\" ON \"Contracts\" (\"GroupId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ImportSessionId\" ON \"Contracts\" (\"ImportSessionId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_MatriculaId\" ON \"Contracts\" (\"MatriculaId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PlanoVendaMetadataId\" ON \"Contracts\" (\"PlanoVendaMetadataId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PvId\" ON \"Contracts\" (\"PvId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_TempMatricula\" ON \"Contracts\" (\"TempMatricula\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_UserInternalId\" ON \"Contracts\" (\"UserInternalId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_IsActive_SaleStartDate\" ON \"Contracts\" (\"IsActive\", \"SaleStartDate\");", suppressTransaction: true);

            // Turn foreign keys back ON
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            // 1. Create temporary table with ContractType
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
    ""RawStatus"" TEXT NULL,
    ""TempMatricula"" TEXT NULL,
    ""TotalAmount"" decimal(18,2) NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    ""UploadId"" TEXT NULL,
    ""UserInternalId"" INTEGER NULL,
    ""Version"" INTEGER NULL,
    ""HasPayment"" INTEGER NULL,
    CONSTRAINT ""FK_Contracts_ContractMetadata_CategoryMetadataId"" FOREIGN KEY (""CategoryMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractMetadata_PlanoVendaMetadataId"" FOREIGN KEY (""PlanoVendaMetadataId"") REFERENCES ""ContractMetadata"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ContractStatuses_ContractStatusId"" FOREIGN KEY (""ContractStatusId"") REFERENCES ""ContractStatuses"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Groups_GroupId"" FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_ImportSessions_ImportSessionId"" FOREIGN KEY (""ImportSessionId"") REFERENCES ""ImportSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Matriculas_MatriculaId"" FOREIGN KEY (""MatriculaId"") REFERENCES ""Matriculas"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_PVs_PvId"" FOREIGN KEY (""PvId"") REFERENCES ""PVs"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Contracts_Users_UserInternalId"" FOREIGN KEY (""UserInternalId"") REFERENCES ""Users"" (""InternalId"") ON DELETE RESTRICT
);", suppressTransaction: true);

            // 2. Copy data back
            migrationBuilder.Sql(@"
INSERT INTO ""Contracts_dg_tmp"" (
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", ""ContractType"",
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""RawStatus"", ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", 
    ""UserInternalId"", ""Version"", ""HasPayment""
)
SELECT 
    ""Id"", ""CategoryMetadataId"", ""ContractNumber"", ""ContractStatusId"", NULL,
    ""CreatedAt"", ""CustomerName"", ""GroupId"", ""ImportSessionId"", ""IsActive"", 
    ""MatriculaId"", ""PlanoVendaMetadataId"", ""PvId"", ""Quota"", ""SaleStartDate"", 
    ""RawStatus"", ""TempMatricula"", ""TotalAmount"", ""UpdatedAt"", ""UploadId"", 
    ""UserInternalId"", ""Version"", ""HasPayment""
FROM ""Contracts"";", suppressTransaction: true);

            // 3. Drop old table & rename
            migrationBuilder.Sql("DROP TABLE \"Contracts\";", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE \"Contracts_dg_tmp\" RENAME TO \"Contracts\";", suppressTransaction: true);

            // 4. Recreate indexes
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_CategoryMetadataId\" ON \"Contracts\" (\"CategoryMetadataId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Contracts_ContractNumber\" ON \"Contracts\" (\"ContractNumber\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ContractStatusId\" ON \"Contracts\" (\"ContractStatusId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_GroupId\" ON \"Contracts\" (\"GroupId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_ImportSessionId\" ON \"Contracts\" (\"ImportSessionId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_MatriculaId\" ON \"Contracts\" (\"MatriculaId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PlanoVendaMetadataId\" ON \"Contracts\" (\"PlanoVendaMetadataId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_PvId\" ON \"Contracts\" (\"PvId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_TempMatricula\" ON \"Contracts\" (\"TempMatricula\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_UserInternalId\" ON \"Contracts\" (\"UserInternalId\");", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX \"IX_Contracts_IsActive_SaleStartDate\" ON \"Contracts\" (\"IsActive\", \"SaleStartDate\");", suppressTransaction: true);

            // Turn foreign keys back ON
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
