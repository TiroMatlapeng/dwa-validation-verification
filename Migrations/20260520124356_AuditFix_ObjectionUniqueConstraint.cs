using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class AuditFix_ObjectionUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Objections_FileMasterId",
                table: "Objections");

            migrationBuilder.CreateIndex(
                name: "IX_Objections_FileMaster_PublicUser_Lodged",
                table: "Objections",
                columns: new[] { "FileMasterId", "PublicUserId" },
                unique: true,
                filter: "[Status] = 'Lodged'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Objections_FileMaster_PublicUser_Lodged",
                table: "Objections");

            migrationBuilder.CreateIndex(
                name: "IX_Objections_FileMasterId",
                table: "Objections",
                column: "FileMasterId");
        }
    }
}
