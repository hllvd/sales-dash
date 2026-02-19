using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImportRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    RowData = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportSessionId",
                table: "ImportRows",
                column: "ImportSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportRows");
        }
    }
}
