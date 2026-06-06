using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_LateFeeBaseOverdueAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BUG-001 fix: freeze accrual base at grace-period-end so partial payments
            // don't corrupt cumulative late fee calculations.
            migrationBuilder.AddColumn<decimal>(
                name: "BaseOverdueAmount",
                table: "LateFees",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill: existing records use OverdueAmount as the base (best approximation).
            migrationBuilder.Sql("UPDATE `LateFees` SET `BaseOverdueAmount` = `OverdueAmount` WHERE `BaseOverdueAmount` = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseOverdueAmount",
                table: "LateFees");
        }
    }
}
