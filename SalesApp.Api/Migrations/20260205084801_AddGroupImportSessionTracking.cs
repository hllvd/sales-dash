using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupImportSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportSessionId",
                table: "Groups",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ImportSessionId",
                table: "Groups",
                column: "ImportSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_ImportSessions_ImportSessionId",
                table: "Groups",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_ImportSessions_ImportSessionId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_ImportSessionId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "Groups");
        }
    }
}
