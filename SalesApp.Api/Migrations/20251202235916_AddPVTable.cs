using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPVTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PvId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PVs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PvId",
                table: "Contracts",
                column: "PvId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_PVs_PvId",
                table: "Contracts",
                column: "PvId",
                principalTable: "PVs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_PVs_PvId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "PVs");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_PvId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PvId",
                table: "Contracts");
        }
    }
}
