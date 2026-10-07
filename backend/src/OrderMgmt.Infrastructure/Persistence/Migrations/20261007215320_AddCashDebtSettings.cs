using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashDebtSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_debt_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    credit_limit_policy = table.Column<int>(type: "integer", nullable: false),
                    negative_fund_policy = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_debt_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "cash_debt_settings",
                columns: new[] { "id", "credit_limit_policy", "negative_fund_policy", "updated_at", "updated_by" },
                values: new object[] { 1, 1, 1, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_debt_settings");
        }
    }
}
