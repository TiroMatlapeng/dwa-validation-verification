using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowGuardsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdditionalInfoReviewedAt",
                table: "FileMasters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DamMarkedNA",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SfraMarkedNA",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SpatialInfoConfirmedAt",
                table: "FileMasters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarmsReviewedAt",
                table: "FileMasters",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalInfoReviewedAt",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "DamMarkedNA",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "SfraMarkedNA",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "SpatialInfoConfirmedAt",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "WarmsReviewedAt",
                table: "FileMasters");
        }
    }
}
