using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReversalAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExceptionType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LoanId = table.Column<int>(type: "int", nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Resolution = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationExceptions_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReconciliationExceptions_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TransactionReversals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OriginalTransactionId = table.Column<int>(type: "int", nullable: false),
                    ReversalTransactionId = table.Column<int>(type: "int", nullable: true),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    ReversalType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ReversedAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_LoanTransactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "LoanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_LoanTransactions_ReversalTransactionId",
                        column: x => x.ReversalTransactionId,
                        principalTable: "LoanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionReversals_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_LoanId",
                table: "ReconciliationExceptions",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationExceptions_ResolvedById",
                table: "ReconciliationExceptions",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_ApprovedByUserId",
                table: "TransactionReversals",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_LoanId",
                table: "TransactionReversals",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_OriginalTransactionId",
                table: "TransactionReversals",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_RequestedByUserId",
                table: "TransactionReversals",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionReversals_ReversalTransactionId",
                table: "TransactionReversals",
                column: "ReversalTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationExceptions");

            migrationBuilder.DropTable(
                name: "TransactionReversals");
        }
    }
}
