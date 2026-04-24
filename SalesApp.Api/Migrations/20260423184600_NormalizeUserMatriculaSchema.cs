using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserMatriculaSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop dependent tables first to avoid FK constraint issues in SQLite
            migrationBuilder.DropTable(name: "Contracts");
            migrationBuilder.DropTable(name: "UserMatriculas");

            // 2. Create the new independent Matriculas table
            migrationBuilder.CreateTable(
                name: "Matriculas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatriculaNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matriculas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matriculas_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 3. Recreate UserMatriculas with the new schema
            migrationBuilder.CreateTable(
                name: "UserMatriculas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatriculaId = table.Column<int>(type: "INTEGER", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMatriculas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMatriculas_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMatriculas_Matriculas_MatriculaId",
                        column: x => x.MatriculaId,
                        principalTable: "Matriculas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMatriculas_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 4. Recreate Contracts with the new schema
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MatriculaId = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "active"),
                    SaleStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    PvId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContractType = table.Column<int>(type: "INTEGER", nullable: true),
                    Quota = table.Column<int>(type: "INTEGER", nullable: true),
                    Version = table.Column<byte>(type: "INTEGER", nullable: true),
                    TempMatricula = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PlanoVendaMetadataId = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryMetadataId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_ContractMetadata_CategoryMetadataId",
                        column: x => x.CategoryMetadataId,
                        principalTable: "ContractMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_ContractMetadata_PlanoVendaMetadataId",
                        column: x => x.PlanoVendaMetadataId,
                        principalTable: "ContractMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Matriculas_MatriculaId",
                        column: x => x.MatriculaId,
                        principalTable: "Matriculas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_PVs_PvId",
                        column: x => x.PvId,
                        principalTable: "PVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 5. Create new indices
            migrationBuilder.CreateIndex(
                name: "IX_Matriculas_MatriculaNumber",
                table: "Matriculas",
                column: "MatriculaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaId",
                table: "UserMatriculas",
                column: "MatriculaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId",
                table: "UserMatriculas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId_MatriculaId",
                table: "UserMatriculas",
                columns: new[] { "UserId", "MatriculaId" },
                unique: true);

            // Filtered index for Single Owner (SQLite compatible syntax)
            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaId_IsOwner",
                table: "UserMatriculas",
                columns: new[] { "MatriculaId", "IsOwner" },
                unique: true,
                filter: "IsOwner = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNumber",
                table: "Contracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_MatriculaId",
                table: "Contracts",
                column: "MatriculaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop tables in reverse order
            migrationBuilder.DropTable(name: "Contracts");
            migrationBuilder.DropTable(name: "UserMatriculas");
            migrationBuilder.DropTable(name: "Matriculas");
        }
    }
}
