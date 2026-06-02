using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ValidationStatusName",
                table: "FileMasters",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_DueDate",
                table: "LetterIssuances",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_IssuedDate",
                table: "LetterIssuances",
                column: "IssuedDate");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_ResponseDate",
                table: "LetterIssuances",
                column: "ResponseDate");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_ValidationStatusName",
                table: "FileMasters",
                column: "ValidationStatusName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_DueDate",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_IssuedDate",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_ResponseDate",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_ValidationStatusName",
                table: "FileMasters");

            migrationBuilder.AlterColumn<string>(
                name: "ValidationStatusName",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
