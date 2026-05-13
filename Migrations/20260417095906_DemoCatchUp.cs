using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class DemoCatchUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProtestDocuments");

            migrationBuilder.DropTable(
                name: "Protests");

            migrationBuilder.AddColumn<bool>(
                name: "IsHDI",
                table: "PublicUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHDI",
                table: "PropertyOwners",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CatchmentAreaId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentPropertyId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyStatus",
                table: "Properties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatchmentAreaId",
                table: "OrganisationalUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesDormantVolume",
                table: "LetterIssuances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "IrrigationBoardId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GovernmentGazetteReference",
                table: "GovernmentWaterControlAreas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProclamationDate",
                table: "GovernmentWaterControlAreas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentTrack",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatchmentAreaId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatchmentAreas",
                columns: table => new
                {
                    CatchmentAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatchmentCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CatchmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatchmentAreas", x => x.CatchmentAreaId);
                    table.ForeignKey(
                        name: "FK_CatchmentAreas_WaterManagementAreas_WmaId",
                        column: x => x.WmaId,
                        principalTable: "WaterManagementAreas",
                        principalColumn: "WmaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GwcaProclamationRules",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterControlAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RuleDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumericLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GovernmentGazetteReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GwcaProclamationRules", x => x.RuleId);
                    table.ForeignKey(
                        name: "FK_GwcaProclamationRules_GovernmentWaterControlAreas_WaterControlAreaId",
                        column: x => x.WaterControlAreaId,
                        principalTable: "GovernmentWaterControlAreas",
                        principalColumn: "WaterControlAreaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mapbooks",
                columns: table => new
                {
                    MapbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapbookTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MapType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GisLayerReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mapbooks", x => x.MapbookId);
                    table.ForeignKey(
                        name: "FK_Mapbooks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mapbooks_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mapbooks_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "Periods",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mapbooks_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Objections",
                columns: table => new
                {
                    ObjectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LodgedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Objections", x => x.ObjectionId);
                    table.ForeignKey(
                        name: "FK_Objections_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Objections_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MapbookImages",
                columns: table => new
                {
                    MapbookImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapbookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SateliteImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayerOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapbookImages", x => x.MapbookImageId);
                    table.ForeignKey(
                        name: "FK_MapbookImages_Mapbooks_MapbookId",
                        column: x => x.MapbookId,
                        principalTable: "Mapbooks",
                        principalColumn: "MapbookId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapbookImages_SateliteImages_SateliteImageId",
                        column: x => x.SateliteImageId,
                        principalTable: "SateliteImages",
                        principalColumn: "ImageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObjectionDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectionDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjectionDocuments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObjectionDocuments_Objections_ObjectionId",
                        column: x => x.ObjectionId,
                        principalTable: "Objections",
                        principalColumn: "ObjectionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CatchmentAreaId",
                table: "Properties",
                column: "CatchmentAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_ParentPropertyId",
                table: "Properties",
                column: "ParentPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationalUnits_CatchmentAreaId",
                table: "OrganisationalUnits",
                column: "CatchmentAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_IrrigationBoardId",
                table: "LetterIssuances",
                column: "IrrigationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_CatchmentAreaId",
                table: "FileMasters",
                column: "CatchmentAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_CatchmentAreas_CatchmentCode",
                table: "CatchmentAreas",
                column: "CatchmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatchmentAreas_WmaId",
                table: "CatchmentAreas",
                column: "WmaId");

            migrationBuilder.CreateIndex(
                name: "IX_GwcaProclamationRules_WaterControlAreaId_RuleCode",
                table: "GwcaProclamationRules",
                columns: new[] { "WaterControlAreaId", "RuleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_MapbookImages_MapbookId",
                table: "MapbookImages",
                column: "MapbookId");

            migrationBuilder.CreateIndex(
                name: "IX_MapbookImages_SateliteImageId",
                table: "MapbookImages",
                column: "SateliteImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Mapbooks_DocumentId",
                table: "Mapbooks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Mapbooks_FileMasterId",
                table: "Mapbooks",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Mapbooks_PeriodId",
                table: "Mapbooks",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Mapbooks_PropertyId",
                table: "Mapbooks",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectionDocuments_DocumentId",
                table: "ObjectionDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectionDocuments_ObjectionId",
                table: "ObjectionDocuments",
                column: "ObjectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Objections_FileMasterId",
                table: "Objections",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Objections_PublicUserId",
                table: "Objections",
                column: "PublicUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_CatchmentAreas_CatchmentAreaId",
                table: "FileMasters",
                column: "CatchmentAreaId",
                principalTable: "CatchmentAreas",
                principalColumn: "CatchmentAreaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_IrrigationBoards_IrrigationBoardId",
                table: "LetterIssuances",
                column: "IrrigationBoardId",
                principalTable: "IrrigationBoards",
                principalColumn: "IrrigationBoardId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganisationalUnits_CatchmentAreas_CatchmentAreaId",
                table: "OrganisationalUnits",
                column: "CatchmentAreaId",
                principalTable: "CatchmentAreas",
                principalColumn: "CatchmentAreaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_CatchmentAreas_CatchmentAreaId",
                table: "Properties",
                column: "CatchmentAreaId",
                principalTable: "CatchmentAreas",
                principalColumn: "CatchmentAreaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Properties_ParentPropertyId",
                table: "Properties",
                column: "ParentPropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_CatchmentAreas_CatchmentAreaId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_IrrigationBoards_IrrigationBoardId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganisationalUnits_CatchmentAreas_CatchmentAreaId",
                table: "OrganisationalUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_Properties_CatchmentAreas_CatchmentAreaId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Properties_ParentPropertyId",
                table: "Properties");

            migrationBuilder.DropTable(
                name: "CatchmentAreas");

            migrationBuilder.DropTable(
                name: "GwcaProclamationRules");

            migrationBuilder.DropTable(
                name: "MapbookImages");

            migrationBuilder.DropTable(
                name: "ObjectionDocuments");

            migrationBuilder.DropTable(
                name: "Mapbooks");

            migrationBuilder.DropTable(
                name: "Objections");

            migrationBuilder.DropIndex(
                name: "IX_Properties_CatchmentAreaId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_ParentPropertyId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationalUnits_CatchmentAreaId",
                table: "OrganisationalUnits");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_IrrigationBoardId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_CatchmentAreaId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "IsHDI",
                table: "PublicUsers");

            migrationBuilder.DropColumn(
                name: "IsHDI",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "CatchmentAreaId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ParentPropertyId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PropertyStatus",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "CatchmentAreaId",
                table: "OrganisationalUnits");

            migrationBuilder.DropColumn(
                name: "IncludesDormantVolume",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "IrrigationBoardId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "GovernmentGazetteReference",
                table: "GovernmentWaterControlAreas");

            migrationBuilder.DropColumn(
                name: "ProclamationDate",
                table: "GovernmentWaterControlAreas");

            migrationBuilder.DropColumn(
                name: "AssessmentTrack",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "CaseNumber",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "CatchmentAreaId",
                table: "FileMasters");

            migrationBuilder.CreateTable(
                name: "Protests",
                columns: table => new
                {
                    ProtestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LodgedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Protests", x => x.ProtestId);
                    table.ForeignKey(
                        name: "FK_Protests_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Protests_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProtestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtestDocuments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProtestDocuments_Protests_ProtestId",
                        column: x => x.ProtestId,
                        principalTable: "Protests",
                        principalColumn: "ProtestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProtestDocuments_DocumentId",
                table: "ProtestDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtestDocuments_ProtestId",
                table: "ProtestDocuments",
                column: "ProtestId");

            migrationBuilder.CreateIndex(
                name: "IX_Protests_FileMasterId",
                table: "Protests",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Protests_PublicUserId",
                table: "Protests",
                column: "PublicUserId");
        }
    }
}
