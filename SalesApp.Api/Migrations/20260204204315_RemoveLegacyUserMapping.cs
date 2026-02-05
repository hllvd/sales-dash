using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyUserMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportUserMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportUserMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolvedUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "pending"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceSurname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportUserMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportUserMappings_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportUserMappings_Users_ResolvedUserId",
                        column: x => x.ResolvedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportUserMappings_ImportSessionId",
                table: "ImportUserMappings",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportUserMappings_ResolvedUserId",
                table: "ImportUserMappings",
                column: "ResolvedUserId");
        }
    }
}
