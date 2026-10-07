using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventorySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    costing_method = table.Column<int>(type: "integer", nullable: false),
                    costing_period = table.Column<int>(type: "integer", nullable: false),
                    costing_scope = table.Column<int>(type: "integer", nullable: false),
                    purchase_cost_includes_vat = table.Column<bool>(type: "boolean", nullable: false),
                    negative_stock_policy = table.Column<int>(type: "integer", nullable: false),
                    net_excludes_vat = table.Column<bool>(type: "boolean", nullable: false),
                    default_date_mode = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "inventory_settings",
                columns: new[] { "id", "costing_method", "costing_period", "costing_scope", "default_date_mode", "negative_stock_policy", "net_excludes_vat", "purchase_cost_includes_vat", "updated_at", "updated_by" },
                values: new object[] { 1, 1, 1, 1, 0, 1, false, true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_settings");
        }
    }
}
