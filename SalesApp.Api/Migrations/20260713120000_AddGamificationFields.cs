using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SalesApp.Data;

#nullable disable

namespace SalesApp.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713120000_AddGamificationFields")]
    /// <inheritdoc />
    public partial class AddGamificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Retention",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextLevelId",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumDirect1LevelId",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumDirect1MinCount",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumDirect2LevelId",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumDirect2MinCount",
                table: "ClassificationLevels",
                type: "INTEGER",
                nullable: true);

            // Note: SQLite does not support AddForeignKey on existing tables.
            // FK relationships are defined in the EF Core model (AppDbContext)
            // and enforced at the application layer. Only indexes are created here.

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationLevels_NextLevelId",
                table: "ClassificationLevels",
                column: "NextLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationLevels_MinimumDirect1LevelId",
                table: "ClassificationLevels",
                column: "MinimumDirect1LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationLevels_MinimumDirect2LevelId",
                table: "ClassificationLevels",
                column: "MinimumDirect2LevelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassificationLevels_NextLevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationLevels_MinimumDirect1LevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationLevels_MinimumDirect2LevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "Retention",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "NextLevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "MinimumDirect1LevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "MinimumDirect1MinCount",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "MinimumDirect2LevelId",
                table: "ClassificationLevels");

            migrationBuilder.DropColumn(
                name: "MinimumDirect2MinCount",
                table: "ClassificationLevels");
        }
    }
}
