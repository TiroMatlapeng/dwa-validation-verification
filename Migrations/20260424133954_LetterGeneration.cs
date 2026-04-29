using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class LetterGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobPath",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServedByOfficialId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureHash",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_ServedByOfficialId",
                table: "LetterIssuances",
                column: "ServedByOfficialId");

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_AspNetUsers_ServedByOfficialId",
                table: "LetterIssuances",
                column: "ServedByOfficialId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_AspNetUsers_ServedByOfficialId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_ServedByOfficialId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "BlobPath",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ServedByOfficialId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "SignatureHash",
                table: "LetterIssuances");
        }
    }
}
