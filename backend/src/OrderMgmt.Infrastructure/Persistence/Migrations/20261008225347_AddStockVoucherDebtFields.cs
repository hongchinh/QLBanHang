using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockVoucherDebtFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                table: "stock_vouchers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fund_id",
                table: "stock_vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_fund_id",
                table: "stock_vouchers",
                column: "fund_id");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_vouchers_funds_fund_id",
                table: "stock_vouchers",
                column: "fund_id",
                principalTable: "funds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_vouchers_funds_fund_id",
                table: "stock_vouchers");

            migrationBuilder.DropIndex(
                name: "ix_stock_vouchers_fund_id",
                table: "stock_vouchers");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "stock_vouchers");

            migrationBuilder.DropColumn(
                name: "fund_id",
                table: "stock_vouchers");
        }
    }
}
