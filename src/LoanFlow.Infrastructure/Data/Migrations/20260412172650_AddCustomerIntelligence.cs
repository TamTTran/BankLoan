using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All schema changes (AssignedToUserId column, CustomerCareNotes table,
            // indexes, and FK constraints) were already applied by EF auto-migration
            // at application startup. This migration is recorded as applied to keep
            // the EF migration history in sync with the actual database schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "CustomerCareNotes");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AssignedToUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Customers");
        }
    }
}
