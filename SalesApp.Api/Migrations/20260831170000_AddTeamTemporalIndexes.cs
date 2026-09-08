using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamTemporalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UserInternalId_SaleStartDate",
                table: "Contracts",
                columns: new[] { "UserInternalId", "SaleStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTeams_TeamId_Dates_UserInternalId",
                table: "UserTeams",
                columns: new[] { "TeamId", "StartDate", "EndDate", "UserInternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_UserInternalId_SaleStartDate",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_UserTeams_TeamId_Dates_UserInternalId",
                table: "UserTeams");
        }
    }
}
