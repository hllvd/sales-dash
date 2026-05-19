using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalUserIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add InternalId to Users as non-nullable with a default value of 0 (SQLite compatible)
            migrationBuilder.AddColumn<int>(
                name: "InternalId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 2. Data Migration: Backfill existing users using rowid
            migrationBuilder.Sql("UPDATE Users SET InternalId = rowid;");

            // 3. Create Unique Index on Users.InternalId instead of AddUniqueConstraint to support SQLite perfectly
            migrationBuilder.CreateIndex(
                name: "IX_Users_InternalId",
                table: "Users",
                column: "InternalId",
                unique: true);

            // 4. Add UserInternalId to Contracts
            migrationBuilder.AddColumn<int>(
                name: "UserInternalId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true);

            // 5. Data Migration: Backfill Contracts.UserInternalId from Users.InternalId via subquery
            migrationBuilder.Sql(@"
                UPDATE Contracts
                SET UserInternalId = (
                    SELECT InternalId FROM Users WHERE Users.Id = Contracts.UserId
                )
                WHERE UserId IS NOT NULL;
            ");

            // 6. Create Index from Contracts.UserInternalId to Users.InternalId
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UserInternalId",
                table: "Contracts",
                column: "UserInternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_UserInternalId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "UserInternalId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Users_InternalId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InternalId",
                table: "Users");
        }
    }
}
