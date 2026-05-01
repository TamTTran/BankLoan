using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNapasTrackingToLoanDisbursement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisbursementStatus",
                table: "LoanDisbursements",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewayConfirmedAt",
                table: "LoanDisbursements",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayErrorMessage",
                table: "LoanDisbursements",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NapasReferenceId",
                table: "LoanDisbursements",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NapasTransactionId",
                table: "LoanDisbursements",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisbursementStatus",
                table: "LoanDisbursements");

            migrationBuilder.DropColumn(
                name: "GatewayConfirmedAt",
                table: "LoanDisbursements");

            migrationBuilder.DropColumn(
                name: "GatewayErrorMessage",
                table: "LoanDisbursements");

            migrationBuilder.DropColumn(
                name: "NapasReferenceId",
                table: "LoanDisbursements");

            migrationBuilder.DropColumn(
                name: "NapasTransactionId",
                table: "LoanDisbursements");
        }
    }
}
