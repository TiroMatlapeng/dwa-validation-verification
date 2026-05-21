using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MfaMethod",
                table: "PublicUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SmsOtps",
                columns: table => new
                {
                    SmsOtpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsOtps", x => x.SmsOtpId);
                    table.ForeignKey(
                        name: "FK_SmsOtps_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                columns: table => new
                {
                    TrustedDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.TrustedDeviceId);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsOtps_PublicUserId_Used_ExpiresAt",
                table: "SmsOtps",
                columns: new[] { "PublicUserId", "Used", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_PublicUserId_ExpiresAt",
                table: "TrustedDevices",
                columns: new[] { "PublicUserId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsOtps");

            migrationBuilder.DropTable(
                name: "TrustedDevices");

            migrationBuilder.DropColumn(
                name: "MfaMethod",
                table: "PublicUsers");
        }
    }
}
