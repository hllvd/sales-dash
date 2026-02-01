using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContractDashboardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryMetadataId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanoVendaMetadataId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TempMatricula",
                table: "Contracts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Version",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CategoryMetadataId",
                table: "Contracts",
                column: "CategoryMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PlanoVendaMetadataId",
                table: "Contracts",
                column: "PlanoVendaMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMetadata_Name_Value",
                table: "ContractMetadata",
                columns: new[] { "Name", "Value" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractMetadata_CategoryMetadataId",
                table: "Contracts",
                column: "CategoryMetadataId",
                principalTable: "ContractMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractMetadata_PlanoVendaMetadataId",
                table: "Contracts",
                column: "PlanoVendaMetadataId",
                principalTable: "ContractMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractMetadata_CategoryMetadataId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractMetadata_PlanoVendaMetadataId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractMetadata");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CategoryMetadataId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_PlanoVendaMetadataId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CategoryMetadataId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PlanoVendaMetadataId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TempMatricula",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Contracts");
        }
    }
}
