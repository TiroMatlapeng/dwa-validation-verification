using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class S33_2_DeclarationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "S33_2_IrrigationBoardId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "S33_2_RatesPaidConfirmed",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "S33_2_ScheduledAreaName",
                table: "FileMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_S33_2_IrrigationBoardId",
                table: "FileMasters",
                column: "S33_2_IrrigationBoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_IrrigationBoards_S33_2_IrrigationBoardId",
                table: "FileMasters",
                column: "S33_2_IrrigationBoardId",
                principalTable: "IrrigationBoards",
                principalColumn: "IrrigationBoardId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_IrrigationBoards_S33_2_IrrigationBoardId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_S33_2_IrrigationBoardId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "S33_2_IrrigationBoardId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "S33_2_RatesPaidConfirmed",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "S33_2_ScheduledAreaName",
                table: "FileMasters");
        }
    }
}
