using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFundTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fund_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_fund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_fund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_fund_transfers", x => x.id);
                    table.CheckConstraint("ck_fund_transfers_distinct_funds", "from_fund_id <> to_fund_id");
                    table.ForeignKey(
                        name: "fk_fund_transfers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fund_transfers_funds_from_fund_id",
                        column: x => x.from_fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fund_transfers_funds_to_fund_id",
                        column: x => x.to_fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fund_transfers_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_branch_id_code",
                table: "fund_transfers",
                columns: new[] { "branch_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_from_fund_id_voucher_at",
                table: "fund_transfers",
                columns: new[] { "from_fund_id", "voucher_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_owner_user_id",
                table: "fund_transfers",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_to_fund_id_voucher_at",
                table: "fund_transfers",
                columns: new[] { "to_fund_id", "voucher_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fund_transfers");
        }
    }
}
