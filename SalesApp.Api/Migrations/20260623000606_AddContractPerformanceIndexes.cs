using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContractPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_IsActive_SaleStartDate",
                table: "Contracts",
                columns: new[] { "IsActive", "SaleStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TempMatricula",
                table: "Contracts",
                column: "TempMatricula");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_IsActive_SaleStartDate",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_TempMatricula",
                table: "Contracts");
        }
    }
}
