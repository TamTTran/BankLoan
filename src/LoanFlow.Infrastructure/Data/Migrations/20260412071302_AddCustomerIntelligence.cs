using System;
using Microsoft.EntityFrameworkCore.Metadata;
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
            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CustomerCareNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    InteractionType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InteractionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextFollowUpDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCareNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerCareNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerCareNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCareNotes_CreatedByUserId",
                table: "CustomerCareNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCareNotes_CustomerId",
                table: "CustomerCareNotes",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");
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
