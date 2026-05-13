using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class FixPropertyCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings");

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings");

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
