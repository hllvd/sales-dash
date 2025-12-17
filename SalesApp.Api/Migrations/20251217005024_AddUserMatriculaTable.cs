using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMatriculaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatriculaId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserMatriculas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatriculaNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMatriculas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMatriculas_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_MatriculaId",
                table: "Contracts",
                column: "MatriculaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaNumber",
                table: "UserMatriculas",
                column: "MatriculaNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId_MatriculaNumber",
                table: "UserMatriculas",
                columns: new[] { "UserId", "MatriculaNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_UserMatriculas_MatriculaId",
                table: "Contracts",
                column: "MatriculaId",
                principalTable: "UserMatriculas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_UserMatriculas_MatriculaId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "UserMatriculas");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_MatriculaId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MatriculaId",
                table: "Contracts");
        }
    }
}
