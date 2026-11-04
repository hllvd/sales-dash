using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class SetUserPreferencesDefaultTrueAndEnableForAllUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET TreatUnpaidActiveAsAwaitingPayment = 1 WHERE TreatUnpaidActiveAsAwaitingPayment = 0;");
            migrationBuilder.Sql("UPDATE Users SET IncludeInactiveUsersInFilter = 1 WHERE IncludeInactiveUsersInFilter = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET TreatUnpaidActiveAsAwaitingPayment = 0;");
            migrationBuilder.Sql("UPDATE Users SET IncludeInactiveUsersInFilter = 0;");
        }
    }
}
