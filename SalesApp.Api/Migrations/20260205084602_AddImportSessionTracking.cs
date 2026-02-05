using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImportSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportSessionId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportSessionId",
                table: "UserMatriculas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportSessionId",
                table: "PVs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportSessionId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ImportSessionId",
                table: "Users",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_ImportSessionId",
                table: "UserMatriculas",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PVs_ImportSessionId",
                table: "PVs",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ImportSessionId",
                table: "Contracts",
                column: "ImportSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ImportSessions_ImportSessionId",
                table: "Contracts",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PVs_ImportSessions_ImportSessionId",
                table: "PVs",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMatriculas_ImportSessions_ImportSessionId",
                table: "UserMatriculas",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ImportSessions_ImportSessionId",
                table: "Users",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ImportSessions_ImportSessionId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_PVs_ImportSessions_ImportSessionId",
                table: "PVs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMatriculas_ImportSessions_ImportSessionId",
                table: "UserMatriculas");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ImportSessions_ImportSessionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ImportSessionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_ImportSessionId",
                table: "UserMatriculas");

            migrationBuilder.DropIndex(
                name: "IX_PVs_ImportSessionId",
                table: "PVs");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ImportSessionId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "UserMatriculas");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "PVs");

            migrationBuilder.DropColumn(
                name: "ImportSessionId",
                table: "Contracts");
        }
    }
}
