using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImportSessionFileData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileData",
                table: "ImportSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileData",
                table: "ImportSessions",
                type: "TEXT",
                nullable: true);
        }
    }
}
