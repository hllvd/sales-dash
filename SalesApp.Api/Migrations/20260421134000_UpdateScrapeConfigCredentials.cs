using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateScrapeConfigCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialStatus",
                table: "ScrapeConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PowerBiPassword",
                table: "ScrapeConfigs",
                type: "TEXT",
                nullable: true);

            // Note: SQLite does not support DropColumn. 
            // We leave these columns in the physical database, but they are no longer mapped in C#.
            /*
            migrationBuilder.DropColumn(
                name: "PowerBiPassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PowerBiUsername",
                table: "Users");
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialStatus",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "PowerBiPassword",
                table: "ScrapeConfigs");

            migrationBuilder.AddColumn<string>(
                name: "PowerBiPassword",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PowerBiUsername",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }
    }
}
