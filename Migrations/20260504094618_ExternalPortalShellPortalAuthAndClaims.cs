using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class ExternalPortalShellPortalAuthAndClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicUserProperties_PublicUserId",
                table: "PublicUserProperties");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_PublicUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_FileMasterId",
                table: "LetterIssuances");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityNumber",
                table: "PublicUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "PublicUsers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "PublicUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HdiConsentGivenDate",
                table: "PublicUsers",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDate",
                table: "PublicUsers",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastUsedOtpTimestamp",
                table: "PublicUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntil",
                table: "PublicUsers",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaEnrolledDate",
                table: "PublicUsers",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecret",
                table: "PublicUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PublicUserProperties",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceDocumentId",
                table: "PublicUserProperties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceType",
                table: "PublicUserProperties",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "PublicUserProperties",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedDate",
                table: "PublicUserProperties",
                type: "datetime2(0)",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityDocumentNumber",
                table: "PropertyOwners",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientPublicUserId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicUserRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    UsedDate = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresDate = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUserRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicUserRecoveryCodes_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicUsers_EmailAddress",
                table: "PublicUsers",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicUsers_IdentityNumber",
                table: "PublicUsers",
                column: "IdentityNumber",
                filter: "[IdentityNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PublicUsers_HdiConsent",
                table: "PublicUsers",
                sql: "[IsHDI] = 0 OR [HdiConsentGivenDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_EvidenceDocumentId",
                table: "PublicUserProperties",
                column: "EvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_Pending",
                table: "PublicUserProperties",
                column: "RequestedDate",
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_UserId_Property_Active",
                table: "PublicUserProperties",
                columns: new[] { "PublicUserId", "PropertyId" },
                unique: true,
                filter: "[Status] <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_UserId_Status",
                table: "PublicUserProperties",
                columns: new[] { "PublicUserId", "Status" })
                .Annotation("SqlServer:Include", new[] { "PropertyId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PublicUserProperties_EvidenceDocumentId",
                table: "PublicUserProperties",
                sql: "[EvidenceType] <> 'TitleDeedUpload' OR [EvidenceDocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyOwners_IdentityDocumentNumber",
                table: "PropertyOwners",
                column: "IdentityDocumentNumber",
                filter: "[IdentityDocumentNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PublicUserId_Unread",
                table: "Notifications",
                column: "PublicUserId",
                filter: "[IsRead] = 0 AND [PublicUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_FileMasterId_IssuedDate",
                table: "LetterIssuances",
                columns: new[] { "FileMasterId", "IssuedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_RecipientPublicUserId",
                table: "LetterIssuances",
                column: "RecipientPublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserRecoveryCodes_PublicUserId_Unused",
                table: "PublicUserRecoveryCodes",
                column: "PublicUserId",
                filter: "[Used] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_PublicUsers_RecipientPublicUserId",
                table: "LetterIssuances",
                column: "RecipientPublicUserId",
                principalTable: "PublicUsers",
                principalColumn: "PublicUserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PublicUserProperties_Documents_EvidenceDocumentId",
                table: "PublicUserProperties",
                column: "EvidenceDocumentId",
                principalTable: "Documents",
                principalColumn: "DocumentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_PublicUsers_RecipientPublicUserId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_PublicUserProperties_Documents_EvidenceDocumentId",
                table: "PublicUserProperties");

            migrationBuilder.DropTable(
                name: "PublicUserRecoveryCodes");

            migrationBuilder.DropIndex(
                name: "IX_PublicUsers_EmailAddress",
                table: "PublicUsers");

            migrationBuilder.DropIndex(
                name: "IX_PublicUsers_IdentityNumber",
                table: "PublicUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PublicUsers_HdiConsent",
                table: "PublicUsers");

            migrationBuilder.DropIndex(
                name: "IX_PublicUserProperties_EvidenceDocumentId",
                table: "PublicUserProperties");

            migrationBuilder.DropIndex(
                name: "IX_PublicUserProperties_Pending",
                table: "PublicUserProperties");

            migrationBuilder.DropIndex(
                name: "IX_PublicUserProperties_UserId_Property_Active",
                table: "PublicUserProperties");

            migrationBuilder.DropIndex(
                name: "IX_PublicUserProperties_UserId_Status",
                table: "PublicUserProperties");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PublicUserProperties_EvidenceDocumentId",
                table: "PublicUserProperties");

            migrationBuilder.DropIndex(
                name: "IX_PropertyOwners_IdentityDocumentNumber",
                table: "PropertyOwners");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_PublicUserId_Unread",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_FileMasterId_IssuedDate",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_RecipientPublicUserId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "HdiConsentGivenDate",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginDate",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "LastUsedOtpTimestamp",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "LockoutUntil",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "MfaEnrolledDate",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "MfaSecret",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "PublicUserProperties");

            migrationBuilder.DropColumn(
                name: "EvidenceType",
                table: "PublicUserProperties");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "PublicUserProperties");

            migrationBuilder.DropColumn(
                name: "RequestedDate",
                table: "PublicUserProperties");

            migrationBuilder.DropColumn(
                name: "RecipientPublicUserId",
                table: "LetterIssuances");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityNumber",
                table: "PublicUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "PublicUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PublicUserProperties",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityDocumentNumber",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_PublicUserId",
                table: "PublicUserProperties",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PublicUserId",
                table: "Notifications",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_FileMasterId",
                table: "LetterIssuances",
                column: "FileMasterId");
        }
    }
}
