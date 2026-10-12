using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchOpeningDate : Migration
    {
        /// Backfill: a branch takes the earliest opening date of its opening stock rows (P8). Run by tests as well.
        public const string BackfillSql =
            "UPDATE branches b SET opening_date = s.d FROM (SELECT branch_id, MIN(opening_date) AS d FROM opening_stocks WHERE is_deleted = false GROUP BY branch_id) s WHERE s.branch_id = b.id;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "opening_date",
                table: "branches",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "branches",
                keyColumn: "id",
                keyValue: new Guid("6f1f7c1e-6a4b-4b53-9a3e-0c0b5a000001"),
                column: "opening_date",
                value: null);

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opening_date",
                table: "branches");
        }
    }
}
