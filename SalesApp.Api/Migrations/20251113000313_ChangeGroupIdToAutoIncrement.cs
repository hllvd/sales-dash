using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGroupIdToAutoIncrement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create new Groups table with auto-increment ID
            migrationBuilder.CreateTable(
                name: "Groups_New",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    Commission = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            // Copy data from old Groups table (excluding Id)
            migrationBuilder.Sql(@"
                INSERT INTO Groups_New (Name, Description, Commission, IsActive, CreatedAt, UpdatedAt)
                SELECT Name, Description, Commission, IsActive, CreatedAt, UpdatedAt
                FROM Groups
                ORDER BY CreatedAt;
            ");

            // Create mapping table for old GUID to new int IDs
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE GroupIdMapping AS
                SELECT 
                    g_old.Id as OldId,
                    g_new.Id as NewId
                FROM Groups g_old
                JOIN Groups_New g_new ON g_old.Name = g_new.Name AND g_old.CreatedAt = g_new.CreatedAt;
            ");

            // Create new Contracts table
            migrationBuilder.CreateTable(
                name: "Contracts_New",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "active"),
                    SaleStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SaleEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups_New",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Copy Contracts data with mapped GroupId
            migrationBuilder.Sql(@"
                INSERT INTO Contracts_New (ContractNumber, UserId, TotalAmount, GroupId, Status, SaleStartDate, SaleEndDate, IsActive, CreatedAt, UpdatedAt)
                SELECT c.ContractNumber, c.UserId, c.TotalAmount, m.NewId, c.Status, c.SaleStartDate, c.SaleEndDate, c.IsActive, c.CreatedAt, c.UpdatedAt
                FROM Contracts c
                JOIN GroupIdMapping m ON c.GroupId = m.OldId
                ORDER BY c.CreatedAt;
            ");

            // Drop old tables
            migrationBuilder.DropTable(name: "Contracts");
            migrationBuilder.DropTable(name: "Groups");

            // Rename new tables
            migrationBuilder.RenameTable(name: "Groups_New", newName: "Groups");
            migrationBuilder.RenameTable(name: "Contracts_New", newName: "Contracts");

            // Create unique index on Name after rename
            migrationBuilder.CreateIndex(
                name: "IX_Groups_Name",
                table: "Groups",
                column: "Name",
                unique: true);

            // Create unique index on ContractNumber after rename
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNumber",
                table: "Contracts",
                column: "ContractNumber",
                unique: true);

            // Create indexes for foreign keys
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_GroupId",
                table: "Contracts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UserId",
                table: "Contracts",
                column: "UserId");

            // Drop temporary mapping table
            migrationBuilder.Sql("DROP TABLE GroupIdMapping;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Groups",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupId",
                table: "Contracts",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
