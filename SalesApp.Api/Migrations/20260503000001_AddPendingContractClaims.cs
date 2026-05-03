using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingContractClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingContractClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatriculaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingContractClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingContractClaims_Matriculas_MatriculaId",
                        column: x => x.MatriculaId,
                        principalTable: "Matriculas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingContractClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingContractClaims_ContractNumber",
                table: "PendingContractClaims",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingContractClaims_IsResolved",
                table: "PendingContractClaims",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_PendingContractClaims_MatriculaId",
                table: "PendingContractClaims",
                column: "MatriculaId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingContractClaims_UserId",
                table: "PendingContractClaims",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PendingContractClaims");
        }
    }
}
