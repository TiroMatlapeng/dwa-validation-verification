using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class Wave2aCalculator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationMethod",
                table: "DamCalculations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContourDifference",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DamArea",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DamDepth",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fetch",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiverDistance",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShapeFactor",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WallLength",
                table: "DamCalculations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CropWaterRates",
                columns: table => new
                {
                    CropWaterRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RatePerHaPerAnnum = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropWaterRates", x => x.CropWaterRateId);
                    table.ForeignKey(
                        name: "FK_CropWaterRates_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "CropId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropWaterRates_IrrigationSystems_IrrigationSystemId",
                        column: x => x.IrrigationSystemId,
                        principalTable: "IrrigationSystems",
                        principalColumn: "IrrigationSystemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SfraSpeciesRates",
                columns: table => new
                {
                    SfraSpeciesRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeciesName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RateM3PerHaPerAnnum = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SfraSpeciesRates", x => x.SfraSpeciesRateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropWaterRates_CropId_IrrigationSystemId",
                table: "CropWaterRates",
                columns: new[] { "CropId", "IrrigationSystemId" },
                unique: true,
                filter: "[IrrigationSystemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CropWaterRates_IrrigationSystemId",
                table: "CropWaterRates",
                column: "IrrigationSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_SfraSpeciesRates_SpeciesName",
                table: "SfraSpeciesRates",
                column: "SpeciesName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropWaterRates");

            migrationBuilder.DropTable(
                name: "SfraSpeciesRates");

            migrationBuilder.DropColumn(
                name: "CalculationMethod",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "ContourDifference",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "DamArea",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "DamDepth",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "Fetch",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "RiverDistance",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "ShapeFactor",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "WallLength",
                table: "DamCalculations");
        }
    }
}
