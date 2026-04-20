using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceLoanTransactionId",
                table: "SuspenseTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourcePaymentId",
                table: "SuspenseTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccrualLoanTransactionId",
                table: "LateFees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    LoanTransactionId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: true),
                    LateFeeId = table.Column<int>(type: "int", nullable: true),
                    AllocationType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_LateFees_LateFeeId",
                        column: x => x.LateFeeId,
                        principalTable: "LateFees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_LoanTransactions_LoanTransactionId",
                        column: x => x.LoanTransactionId,
                        principalTable: "LoanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_RepaymentSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "RepaymentSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SuspenseTransactions_SourceLoanTransactionId",
                table: "SuspenseTransactions",
                column: "SourceLoanTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspenseTransactions_SourcePaymentId",
                table: "SuspenseTransactions",
                column: "SourcePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_LateFeeId",
                table: "PaymentAllocations",
                column: "LateFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_LoanTransactionId",
                table: "PaymentAllocations",
                column: "LoanTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_ScheduleId",
                table: "PaymentAllocations",
                column: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_SuspenseTransactions_LoanTransactions_SourceLoanTransactionId",
                table: "SuspenseTransactions",
                column: "SourceLoanTransactionId",
                principalTable: "LoanTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuspenseTransactions_Payments_SourcePaymentId",
                table: "SuspenseTransactions",
                column: "SourcePaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SuspenseTransactions_LoanTransactions_SourceLoanTransactionId",
                table: "SuspenseTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SuspenseTransactions_Payments_SourcePaymentId",
                table: "SuspenseTransactions");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_SuspenseTransactions_SourceLoanTransactionId",
                table: "SuspenseTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SuspenseTransactions_SourcePaymentId",
                table: "SuspenseTransactions");

            migrationBuilder.DropColumn(
                name: "SourceLoanTransactionId",
                table: "SuspenseTransactions");

            migrationBuilder.DropColumn(
                name: "SourcePaymentId",
                table: "SuspenseTransactions");

            migrationBuilder.DropColumn(
                name: "AccrualLoanTransactionId",
                table: "LateFees");
        }
    }
}
