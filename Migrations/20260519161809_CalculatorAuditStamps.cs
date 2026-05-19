using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class CalculatorAuditStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCalculatedAt",
                table: "Forestations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCalculatedAt",
                table: "FieldAndCrops",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCalculatedAt",
                table: "DamCalculations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCalculatedAt",
                table: "Forestations");

            migrationBuilder.DropColumn(
                name: "LastCalculatedAt",
                table: "FieldAndCrops");

            migrationBuilder.DropColumn(
                name: "LastCalculatedAt",
                table: "DamCalculations");
        }
    }
}
