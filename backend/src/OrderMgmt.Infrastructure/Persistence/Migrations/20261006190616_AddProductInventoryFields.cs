using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInventoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cost_price_updated_on",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "price_includes_vat",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_discount_rate",
                table: "products",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "sales_discount_rate",
                table: "products",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "track_inventory",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Service products (group VC "Vận chuyển") do not track stock; the real catalog is reviewed before go-live.
            migrationBuilder.Sql("UPDATE products SET track_inventory = false WHERE product_group_id IN (SELECT id FROM product_groups WHERE code = 'VC');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cost_price_updated_on",
                table: "products");

            migrationBuilder.DropColumn(
                name: "price_includes_vat",
                table: "products");

            migrationBuilder.DropColumn(
                name: "purchase_discount_rate",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sales_discount_rate",
                table: "products");

            migrationBuilder.DropColumn(
                name: "track_inventory",
                table: "products");
        }
    }
}
