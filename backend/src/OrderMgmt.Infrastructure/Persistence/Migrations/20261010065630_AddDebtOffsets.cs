using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtOffsets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "debt_offset_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_debt_offset_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_debt_offset_vouchers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_debt_offset_vouchers_customers_partner_id",
                        column: x => x.partner_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_debt_offset_vouchers_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_debt_offset_vouchers_branch_id_code",
                table: "debt_offset_vouchers",
                columns: new[] { "branch_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_debt_offset_vouchers_branch_id_voucher_at",
                table: "debt_offset_vouchers",
                columns: new[] { "branch_id", "voucher_at" });

            migrationBuilder.CreateIndex(
                name: "ix_debt_offset_vouchers_owner_user_id",
                table: "debt_offset_vouchers",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_offset_vouchers_partner_id",
                table: "debt_offset_vouchers",
                column: "partner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "debt_offset_vouchers");
        }
    }
}
