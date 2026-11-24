using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanAccessPowerBiScrapeToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanAccessPowerBiScrape",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanAccessPowerBiScrape",
                table: "Users");
        }
    }
}
