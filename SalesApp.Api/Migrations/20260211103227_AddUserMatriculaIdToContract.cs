using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMatriculaIdToContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserMatriculaId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UserMatriculaId",
                table: "Contracts",
                column: "UserMatriculaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_UserMatriculas_UserMatriculaId",
                table: "Contracts",
                column: "UserMatriculaId",
                principalTable: "UserMatriculas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_UserMatriculas_UserMatriculaId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_UserMatriculaId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "UserMatriculaId",
                table: "Contracts");
        }
    }
}
