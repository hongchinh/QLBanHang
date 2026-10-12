using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodFundKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fund_kind",
                table: "payment_methods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // FundKind: None = 0, Cash = 1, Bank = 2. Seeded 'CK' becomes Bank (P9).
            migrationBuilder.Sql("UPDATE payment_methods SET fund_kind = 1 WHERE is_cash;");
            migrationBuilder.Sql("UPDATE payment_methods SET fund_kind = 2 WHERE code = 'CK' AND NOT is_cash;");

            migrationBuilder.DropColumn(
                name: "is_cash",
                table: "payment_methods");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_cash",
                table: "payment_methods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE payment_methods SET is_cash = (fund_kind = 1);");

            migrationBuilder.DropColumn(
                name: "fund_kind",
                table: "payment_methods");
        }
    }
}
