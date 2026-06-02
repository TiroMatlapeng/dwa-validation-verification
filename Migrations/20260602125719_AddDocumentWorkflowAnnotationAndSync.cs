using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentWorkflowAnnotationAndSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalDocumentRef",
                table: "Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NotSynced");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStateId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkflowStateId",
                table: "Documents",
                column: "WorkflowStateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_WorkflowStates_WorkflowStateId",
                table: "Documents",
                column: "WorkflowStateId",
                principalTable: "WorkflowStates",
                principalColumn: "WorkflowStateId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_WorkflowStates_WorkflowStateId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkflowStateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExternalDocumentRef",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "WorkflowStateId",
                table: "Documents");
        }
    }
}
