using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeConfigImportOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SkipMissingContractNumber",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowAutoCreateGroups",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowAutoCreatePVs",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateMatriculaOnExisting",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateTotalAmountOnExisting",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateStartDateOnExisting",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkipMissingContractNumber",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "AllowAutoCreateGroups",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "AllowAutoCreatePVs",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "UpdateMatriculaOnExisting",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "UpdateTotalAmountOnExisting",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "UpdateStartDateOnExisting",
                table: "ScrapeConfigs");
        }
    }
}
