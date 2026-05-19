using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class Wave2bLawfulness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IrrigableAreaHa",
                table: "Properties",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WaterControlAreaId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LawfulnessAssessmentResults",
                columns: table => new
                {
                    LawfulnessAssessmentResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalFramework = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GwcaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalIrrigatedAreaHa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalIrrigationDemandM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LawfulIrrigationM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnlawfulIrrigationM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IrrigationLimitApplied = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalDamCapacityM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LawfulStorageM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnlawfulStorageM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StorageLimitApplied = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawfulnessAssessmentResults", x => x.LawfulnessAssessmentResultId);
                    table.ForeignKey(
                        name: "FK_LawfulnessAssessmentResults_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LawfulnessAssessmentResults_GovernmentWaterControlAreas_GwcaId",
                        column: x => x.GwcaId,
                        principalTable: "GovernmentWaterControlAreas",
                        principalColumn: "WaterControlAreaId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_WaterControlAreaId",
                table: "Properties",
                column: "WaterControlAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_LawfulnessAssessmentResults_FileMasterId",
                table: "LawfulnessAssessmentResults",
                column: "FileMasterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LawfulnessAssessmentResults_GwcaId",
                table: "LawfulnessAssessmentResults",
                column: "GwcaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_GovernmentWaterControlAreas_WaterControlAreaId",
                table: "Properties",
                column: "WaterControlAreaId",
                principalTable: "GovernmentWaterControlAreas",
                principalColumn: "WaterControlAreaId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_GovernmentWaterControlAreas_WaterControlAreaId",
                table: "Properties");

            migrationBuilder.DropTable(
                name: "LawfulnessAssessmentResults");

            migrationBuilder.DropIndex(
                name: "IX_Properties_WaterControlAreaId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IrrigableAreaHa",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WaterControlAreaId",
                table: "Properties");
        }
    }
}
