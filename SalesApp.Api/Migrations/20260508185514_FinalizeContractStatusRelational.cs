using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeContractStatusRelational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- AUTOMATED DATA MIGRATION ---
            // Populate the lookup table and update all contracts BEFORE the column is dropped
            migrationBuilder.Sql(@"
                INSERT INTO ContractStatuses (Name)
                SELECT DISTINCT Status FROM Contracts
                WHERE Status IS NOT NULL AND Status NOT IN (SELECT Name FROM ContractStatuses);

                UPDATE Contracts
                SET ContractStatusId = (
                    SELECT Id FROM ContractStatuses WHERE Name = Contracts.Status
                )
                WHERE ContractStatusId IS NULL;
            ");
            // --------------------------------

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_MatriculaId",
                table: "UserMatriculas");

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_MatriculaId_IsOwner",
                table: "UserMatriculas");

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_UserId",
                table: "UserMatriculas");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Matriculas");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Contracts");

            migrationBuilder.AlterColumn<bool>(
                name: "IsResolved",
                table: "PendingContractClaims",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "ContractStatusId",
                table: "Contracts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaId",
                table: "UserMatriculas",
                column: "MatriculaId",
                unique: true,
                filter: "[IsOwner] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Matriculas_ImportSessions_ImportSessionId",
                table: "Matriculas",
                column: "ImportSessionId",
                principalTable: "ImportSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matriculas_ImportSessions_ImportSessionId",
                table: "Matriculas");

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_MatriculaId",
                table: "UserMatriculas");

            migrationBuilder.DropIndex(
                name: "IX_UserMatriculas_UserId_MatriculaId",
                table: "UserMatriculas");

            migrationBuilder.AlterColumn<bool>(
                name: "IsResolved",
                table: "PendingContractClaims",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Matriculas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ContractStatusId",
                table: "Contracts",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Contracts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaId",
                table: "UserMatriculas",
                column: "MatriculaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_MatriculaId_IsOwner",
                table: "UserMatriculas",
                columns: new[] { "MatriculaId", "IsOwner" },
                unique: true,
                filter: "IsOwner = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserMatriculas_UserId",
                table: "UserMatriculas",
                column: "UserId");
        }
    }
}
