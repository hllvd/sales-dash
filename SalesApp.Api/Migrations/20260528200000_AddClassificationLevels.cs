using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SalesApp.Data;

#nullable disable

namespace SalesApp.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260528200000_AddClassificationLevels")]
    /// <inheritdoc />
    public partial class AddClassificationLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassificationLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Prize = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SalesGoal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserInternalId = table.Column<int>(type: "INTEGER", nullable: false),
                    LevelId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClassifications_ClassificationLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "ClassificationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserClassifications_Users_UserInternalId",
                        column: x => x.UserInternalId,
                        principalTable: "Users",
                        principalColumn: "InternalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationLevels_Name",
                table: "ClassificationLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClassifications_LevelId",
                table: "UserClassifications",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClassifications_UserInternalId_EndDate",
                table: "UserClassifications",
                columns: new[] { "UserInternalId", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserClassifications");

            migrationBuilder.DropTable(
                name: "ClassificationLevels");
        }
    }
}
