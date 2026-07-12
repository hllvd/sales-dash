using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesApp.Data;

#nullable disable

namespace SalesApp.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260712100200_AddNaoDefinidoStatus")]
    public partial class AddNaoDefinidoStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawStatus",
                table: "Contracts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Pre-populate the ContractStatuses table with "NaoDefinido" to ensure it exists.
            migrationBuilder.Sql(@"
                INSERT INTO ContractStatuses (Name)
                SELECT 'NaoDefinido'
                WHERE NOT EXISTS (SELECT 1 FROM ContractStatuses WHERE Name = 'NaoDefinido');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawStatus",
                table: "Contracts");

            migrationBuilder.Sql("DELETE FROM ContractStatuses WHERE Name = 'NaoDefinido';");
        }
    }
}
