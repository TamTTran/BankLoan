using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations;

[DbContext(typeof(LoanFlowDbContext))]
[Migration("20260420143000_EnforceRowVersionSchema")]
public class EnforceRowVersionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE `Loans` MODIFY COLUMN `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `RepaymentSchedules` MODIFY COLUMN `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `LateFees` MODIFY COLUMN `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `TransactionReversals` MODIFY COLUMN `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE `Loans` MODIFY COLUMN `RowVersion` datetime(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `RepaymentSchedules` MODIFY COLUMN `RowVersion` datetime(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `LateFees` MODIFY COLUMN `RowVersion` datetime(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
        migrationBuilder.Sql(
            "ALTER TABLE `TransactionReversals` MODIFY COLUMN `RowVersion` datetime(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
    }
}
