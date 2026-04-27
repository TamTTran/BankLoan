using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NhnnClassificationUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Loans",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IndustryCode",
                table: "Loans",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PurposeCode",
                table: "Loans",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "QualitativeClassification",
                table: "Loans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualitativeReason",
                table: "Loans",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IndustryCode",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "PurposeCode",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "QualitativeClassification",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "QualitativeReason",
                table: "Loans");
        }
    }
}
