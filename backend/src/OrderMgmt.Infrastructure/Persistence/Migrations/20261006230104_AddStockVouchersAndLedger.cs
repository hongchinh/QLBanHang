using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockVouchersAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_counters",
                columns: table => new
                {
                    doc_type = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_counters", x => new { x.doc_type, x.branch_id, x.period_key });
                });

            migrationBuilder.CreateTable(
                name: "inventory_cost_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_key = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    opening_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    opening_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    in_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    in_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    out_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    out_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    avg_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    closing_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    closing_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_cost_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    line_sort_order = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty_in = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    qty_out = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    in_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    running_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    cost_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_ledger", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "opening_stocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_stocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_opening_stocks_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opening_stocks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opening_stocks_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_balances", x => new { x.warehouse_id, x.product_id });
                });

            migrationBuilder.CreateTable(
                name: "stock_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    partner_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    partner_tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    handler_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reason_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    freight = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    order_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    goods_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_discount_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_customers_partner_id",
                        column: x => x.partner_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_stock_reasons_reason_id",
                        column: x => x.reason_id,
                        principalTable: "stock_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_vouchers_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_voucher_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_voucher_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_voucher_activities_stock_vouchers_stock_voucher_id",
                        column: x => x.stock_voucher_id,
                        principalTable: "stock_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_voucher_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_inventory = table.Column<bool>(type: "boolean", nullable: false),
                    pricing_mode = table.Column<int>(type: "integer", nullable: false),
                    unit_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_includes_vat = table.Column<bool>(type: "boolean", nullable: false),
                    sheet_count = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    length = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    width = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    thickness = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_manual = table.Column<bool>(type: "boolean", nullable: false),
                    order_discount_allocated = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    freight_allocated = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    inbound_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_voucher_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_voucher_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_voucher_lines_stock_vouchers_stock_voucher_id",
                        column: x => x.stock_voucher_id,
                        principalTable: "stock_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_voucher_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_cost_periods_branch_id_period_start",
                table: "inventory_cost_periods",
                columns: new[] { "branch_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_cost_periods_product_id_scope_key_period_start",
                table: "inventory_cost_periods",
                columns: new[] { "product_id", "scope_key", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_ledger_product_id_branch_id_posted_at",
                table: "inventory_ledger",
                columns: new[] { "product_id", "branch_id", "posted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_ledger_product_id_warehouse_id_posted_at",
                table: "inventory_ledger",
                columns: new[] { "product_id", "warehouse_id", "posted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_ledger_source_type_source_id",
                table: "inventory_ledger",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opening_stocks_branch_id",
                table: "opening_stocks",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_stocks_product_id",
                table: "opening_stocks",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_stocks_warehouse_id_product_id",
                table: "opening_stocks",
                columns: new[] { "warehouse_id", "product_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_stock_voucher_activities_actor_user_id",
                table: "stock_voucher_activities",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_voucher_activities_voucher_occurred",
                table: "stock_voucher_activities",
                columns: new[] { "stock_voucher_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_stock_voucher_lines_product_id",
                table: "stock_voucher_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_voucher_lines_stock_voucher_id",
                table: "stock_voucher_lines",
                column: "stock_voucher_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_voucher_lines_warehouse_id",
                table: "stock_voucher_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_branch_id_type_voucher_at",
                table: "stock_vouchers",
                columns: new[] { "branch_id", "type", "voucher_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_owner_user_id",
                table: "stock_vouchers",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_partner_id",
                table: "stock_vouchers",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_payment_method_id",
                table: "stock_vouchers",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_reason_id",
                table: "stock_vouchers",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_type_branch_id_code",
                table: "stock_vouchers",
                columns: new[] { "type", "branch_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_stock_vouchers_warehouse_id",
                table: "stock_vouchers",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_counters");

            migrationBuilder.DropTable(
                name: "inventory_cost_periods");

            migrationBuilder.DropTable(
                name: "inventory_ledger");

            migrationBuilder.DropTable(
                name: "opening_stocks");

            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropTable(
                name: "stock_voucher_activities");

            migrationBuilder.DropTable(
                name: "stock_voucher_lines");

            migrationBuilder.DropTable(
                name: "stock_vouchers");
        }
    }
}
