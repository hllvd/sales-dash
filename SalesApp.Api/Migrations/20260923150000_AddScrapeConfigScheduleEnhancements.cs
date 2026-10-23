using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeConfigScheduleEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScheduleMode",
                table: "ScrapeConfigs",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleIntervalHours",
                table: "ScrapeConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleTimes",
                table: "ScrapeConfigs",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            // Migrar contas existentes que já tinham ScrapeIntervalHours para o novo ScheduleMode = 'interval'
            migrationBuilder.Sql("UPDATE ScrapeConfigs SET ScheduleMode = 'interval', ScheduleIntervalHours = ScrapeIntervalHours WHERE ScrapeIntervalHours IS NOT NULL AND ScrapeIntervalHours >= 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "ScheduleIntervalHours",
                table: "ScrapeConfigs");

            migrationBuilder.DropColumn(
                name: "ScheduleTimes",
                table: "ScrapeConfigs");
        }
    }
}
