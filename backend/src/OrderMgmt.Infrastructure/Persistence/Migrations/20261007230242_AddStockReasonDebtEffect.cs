using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReasonDebtEffect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "debt_effect",
                table: "stock_reasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ReceivableIncrease = 2 for XBH, PayableIncrease = 4 for NMH.
            migrationBuilder.Sql("UPDATE stock_reasons SET debt_effect = 2 WHERE code = 'XBH';");
            migrationBuilder.Sql("UPDATE stock_reasons SET debt_effect = 4 WHERE code = 'NMH';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "debt_effect",
                table: "stock_reasons");
        }
    }
}
