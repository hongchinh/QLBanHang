using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_sample",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_sample",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sample_data_error",
                table: "branches",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sample_data_progress",
                table: "branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "sample_data_status",
                table: "branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "branches",
                keyColumn: "id",
                keyValue: new Guid("6f1f7c1e-6a4b-4b53-9a3e-0c0b5a000001"),
                column: "sample_data_error",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_sample",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_sample",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "sample_data_error",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "sample_data_progress",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "sample_data_status",
                table: "branches");
        }
    }
}
