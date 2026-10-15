using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFundBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_holder_name",
                table: "funds",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bank_id",
                table: "funds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_funds_bank_id",
                table: "funds",
                column: "bank_id");

            migrationBuilder.AddForeignKey(
                name: "fk_funds_banks_bank_id",
                table: "funds",
                column: "bank_id",
                principalTable: "banks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_funds_banks_bank_id",
                table: "funds");

            migrationBuilder.DropIndex(
                name: "ix_funds_bank_id",
                table: "funds");

            migrationBuilder.DropColumn(
                name: "account_holder_name",
                table: "funds");

            migrationBuilder.DropColumn(
                name: "bank_id",
                table: "funds");
        }
    }
}
