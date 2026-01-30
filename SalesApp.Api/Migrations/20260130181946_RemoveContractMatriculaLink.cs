using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContractMatriculaLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_UserMatriculas_MatriculaId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_MatriculaId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MatriculaId",
                table: "Contracts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatriculaId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_MatriculaId",
                table: "Contracts",
                column: "MatriculaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_UserMatriculas_MatriculaId",
                table: "Contracts",
                column: "MatriculaId",
                principalTable: "UserMatriculas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
