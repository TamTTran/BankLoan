using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BankGradeFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EarlySettlementFeeRate",
                table: "Loans",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriodMonths",
                table: "Loans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InterestMethod",
                table: "Loans",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Flat")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LateFeeGraceDays",
                table: "Loans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeRate",
                table: "Loans",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "Loans",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PersonalUnsecured")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PromotionalMonths",
                table: "Loans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionalRate",
                table: "Loans",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "LoanProducts",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "EarlySettlementFeeRate",
                table: "LoanProducts",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriodMonths",
                table: "LoanProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InterestMethod",
                table: "LoanProducts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Flat")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LateFeeGraceDays",
                table: "LoanProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeRate",
                table: "LoanProducts",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxLtvRatio",
                table: "LoanProducts",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "LoanProducts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PersonalUnsecured")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PromotionalMonths",
                table: "LoanProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionalRate",
                table: "LoanProducts",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCollateral",
                table: "LoanProducts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApprovalLevel",
                table: "LoanApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalLevel",
                table: "LoanApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditScore",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditStatus",
                table: "Customers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ApprovalRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProductType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredLevel = table.Column<int>(type: "int", nullable: false),
                    ApproverRole = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRules", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Collaterals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CollateralType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegistrationNo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppraisedBy = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Active")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaterals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collaterals_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CreditChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: true),
                    TotalLoans = table.Column<int>(type: "int", nullable: false),
                    ActiveLoans = table.Column<int>(type: "int", nullable: false),
                    OverdueLoans = table.Column<int>(type: "int", nullable: false),
                    ClosedLoans = table.Column<int>(type: "int", nullable: false),
                    TotalOutstanding = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDpdLast12Months = table.Column<int>(type: "int", nullable: false),
                    CreditScore = table.Column<int>(type: "int", nullable: false),
                    CreditGrade = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Recommendation = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CheckedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditChecks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditChecks_Users_CheckedByUserId",
                        column: x => x.CheckedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LateFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    OverdueAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    FeeRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateFees_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateFees_RepaymentSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "RepaymentSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LoanCollaterals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    CollateralId = table.Column<int>(type: "int", nullable: false),
                    AllocatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanCollaterals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanCollaterals_Collaterals_CollateralId",
                        column: x => x.CollateralId,
                        principalTable: "Collaterals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanCollaterals_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Collaterals_CustomerId",
                table: "Collaterals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditChecks_CheckedByUserId",
                table: "CreditChecks",
                column: "CheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditChecks_CustomerId",
                table: "CreditChecks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFees_LoanId",
                table: "LateFees",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFees_ScheduleId",
                table: "LateFees",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCollaterals_CollateralId",
                table: "LoanCollaterals",
                column: "CollateralId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCollaterals_LoanId",
                table: "LoanCollaterals",
                column: "LoanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRules");

            migrationBuilder.DropTable(
                name: "CreditChecks");

            migrationBuilder.DropTable(
                name: "LateFees");

            migrationBuilder.DropTable(
                name: "LoanCollaterals");

            migrationBuilder.DropTable(
                name: "Collaterals");

            migrationBuilder.DropColumn(
                name: "EarlySettlementFeeRate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "GracePeriodMonths",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "InterestMethod",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LateFeeGraceDays",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LateFeeRate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "PromotionalMonths",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "PromotionalRate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "EarlySettlementFeeRate",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "GracePeriodMonths",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "InterestMethod",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "LateFeeGraceDays",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "LateFeeRate",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "MaxLtvRatio",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "PromotionalMonths",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "PromotionalRate",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "RequiresCollateral",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "CurrentApprovalLevel",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalLevel",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "CreditScore",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditStatus",
                table: "Customers");
        }
    }
}
