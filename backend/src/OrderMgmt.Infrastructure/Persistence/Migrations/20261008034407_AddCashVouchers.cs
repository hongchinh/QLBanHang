using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_document_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_document_activities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cash_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    partner_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    partner_tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    person_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attached_docs = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_stock_voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_cash_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_cash_reasons_reason_id",
                        column: x => x.reason_id,
                        principalTable: "cash_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_customers_partner_id",
                        column: x => x.partner_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_funds_fund_id",
                        column: x => x.fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_stock_vouchers_source_stock_voucher_id",
                        column: x => x.source_stock_voucher_id,
                        principalTable: "stock_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_vouchers_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_voucher_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("pk_cash_voucher_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_voucher_lines_cash_vouchers_cash_voucher_id",
                        column: x => x.cash_voucher_id,
                        principalTable: "cash_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_document_activities_document_type_document_id_occurred",
                table: "cash_document_activities",
                columns: new[] { "document_type", "document_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_voucher_lines_cash_voucher_id",
                table: "cash_voucher_lines",
                column: "cash_voucher_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_branch_id_type_voucher_at",
                table: "cash_vouchers",
                columns: new[] { "branch_id", "type", "voucher_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_fund_id_voucher_at",
                table: "cash_vouchers",
                columns: new[] { "fund_id", "voucher_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_owner_user_id",
                table: "cash_vouchers",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_partner_id",
                table: "cash_vouchers",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_reason_id",
                table: "cash_vouchers",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_source_stock_voucher_id",
                table: "cash_vouchers",
                column: "source_stock_voucher_id",
                unique: true,
                filter: "is_deleted = false AND source_stock_voucher_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cash_vouchers_type_branch_id_code",
                table: "cash_vouchers",
                columns: new[] { "type", "branch_id", "code" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_document_activities");

            migrationBuilder.DropTable(
                name: "cash_voucher_lines");

            migrationBuilder.DropTable(
                name: "cash_vouchers");
        }
    }
}
