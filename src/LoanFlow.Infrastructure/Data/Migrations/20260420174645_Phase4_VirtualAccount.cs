using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_VirtualAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReferenceId",
                table: "Payments",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VirtualAccountNo",
                table: "Loans",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "LivenessScore",
                table: "EkycVerifications",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Customers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "Customers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EkycStatus",
                table: "Customers",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EkycVerifiedAt",
                table: "Customers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoApprove",
                table: "ApprovalRules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinCicScore",
                table: "ApprovalRules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    ContractNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PdfContentHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MockDigitalSignature = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientIpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EContracts_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ExternalReferenceId",
                table: "Payments",
                column: "ExternalReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_VirtualAccountNo",
                table: "Loans",
                column: "VirtualAccountNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EContracts_LoanId",
                table: "EContracts",
                column: "LoanId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EContracts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ExternalReferenceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Loans_VirtualAccountNo",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ExternalReferenceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VirtualAccountNo",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LivenessScore",
                table: "EkycVerifications");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EkycStatus",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EkycVerifiedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsAutoApprove",
                table: "ApprovalRules");

            migrationBuilder.DropColumn(
                name: "MinCicScore",
                table: "ApprovalRules");
        }
    }
}
