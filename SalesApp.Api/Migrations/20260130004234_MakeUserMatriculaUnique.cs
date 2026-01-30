using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserMatriculaUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_UserId_MatriculaNumber",
                table: "UserMatriculas");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId_MatriculaNumber",
                table: "UserMatriculas",
                columns: new[] { "UserId", "MatriculaNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_UserId_MatriculaNumber",
                table: "UserMatriculas");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId_MatriculaNumber",
                table: "UserMatriculas",
                columns: new[] { "UserId", "MatriculaNumber" });
        }
    }
}
