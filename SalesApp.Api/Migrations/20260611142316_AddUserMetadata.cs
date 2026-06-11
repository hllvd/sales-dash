using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMetadataFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GroupLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FieldType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "text"),
                    DropdownOptions = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMetadataFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMetadataValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserInternalId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserMetadataFieldId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMetadataValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMetadataValues_UserMetadataFields_UserMetadataFieldId",
                        column: x => x.UserMetadataFieldId,
                        principalTable: "UserMetadataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMetadataValues_Users_UserInternalId",
                        column: x => x.UserInternalId,
                        principalTable: "Users",
                        principalColumn: "InternalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMetadataFields_Key",
                table: "UserMetadataFields",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMetadataValues_UserInternalId_UserMetadataFieldId",
                table: "UserMetadataValues",
                columns: new[] { "UserInternalId", "UserMetadataFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMetadataValues_UserMetadataFieldId",
                table: "UserMetadataValues",
                column: "UserMetadataFieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMetadataValues");

            migrationBuilder.DropTable(
                name: "UserMetadataFields");
        }
    }
}
