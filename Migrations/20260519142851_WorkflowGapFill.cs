using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowGapFill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ServiceConfirmedDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrePublicReviewApprovedAt",
                table: "FileMasters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrePublicReviewApprovedById",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StakeholderWorkshopAttendance",
                table: "FileMasters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StakeholderWorkshopDate",
                table: "FileMasters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StakeholderWorkshopVenue",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PAJAChecklists",
                columns: table => new
                {
                    PAJAChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactualBasis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalBasis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserInputConsideration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalReasoning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAJAChecklists", x => x.PAJAChecklistId);
                    table.ForeignKey(
                        name: "FK_PAJAChecklists_AspNetUsers_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAJAChecklists_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_PrePublicReviewApprovedById",
                table: "FileMasters",
                column: "PrePublicReviewApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PAJAChecklists_CompletedById",
                table: "PAJAChecklists",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_PAJAChecklists_FileMasterId",
                table: "PAJAChecklists",
                column: "FileMasterId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_AspNetUsers_PrePublicReviewApprovedById",
                table: "FileMasters",
                column: "PrePublicReviewApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_AspNetUsers_PrePublicReviewApprovedById",
                table: "FileMasters");

            migrationBuilder.DropTable(
                name: "PAJAChecklists");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_PrePublicReviewApprovedById",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "ServiceConfirmedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PrePublicReviewApprovedAt",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "PrePublicReviewApprovedById",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "StakeholderWorkshopAttendance",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "StakeholderWorkshopDate",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "StakeholderWorkshopVenue",
                table: "FileMasters");
        }
    }
}
