using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "debt_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    doc_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debt_entries", x => x.id);
                    table.CheckConstraint("ck_debt_entries_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_debt_entries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_debt_entries_customers_partner_id",
                        column: x => x.partner_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debt_settlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    increase_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decrease_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    origin = table.Column<int>(type: "integer", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debt_settlements", x => x.id);
                    table.CheckConstraint("ck_debt_settlements_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_debt_settlements_debt_entries_decrease_entry_id",
                        column: x => x.decrease_entry_id,
                        principalTable: "debt_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_debt_settlements_debt_entries_increase_entry_id",
                        column: x => x.increase_entry_id,
                        principalTable: "debt_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_debt_entries_branch_id_partner_id_side_posted_at",
                table: "debt_entries",
                columns: new[] { "branch_id", "partner_id", "side", "posted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_debt_entries_partner_id",
                table: "debt_entries",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_entries_source_type_source_id_side",
                table: "debt_entries",
                columns: new[] { "source_type", "source_id", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_debt_settlements_batch_id",
                table: "debt_settlements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_settlements_decrease_entry_id",
                table: "debt_settlements",
                column: "decrease_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_settlements_increase_entry_id",
                table: "debt_settlements",
                column: "increase_entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "debt_settlements");

            migrationBuilder.DropTable(
                name: "debt_entries");
        }
    }
}
