using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class PropertyLineageSuccessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SuccessorPropertyId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_SuccessorPropertyId",
                table: "Properties",
                column: "SuccessorPropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Properties_SuccessorPropertyId",
                table: "Properties",
                column: "SuccessorPropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Properties_SuccessorPropertyId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_SuccessorPropertyId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SuccessorPropertyId",
                table: "Properties");
        }
    }
}
