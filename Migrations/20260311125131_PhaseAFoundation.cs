using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class PhaseAFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Crops_CropTypes_CropTypeId",
                table: "Crops");

            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Rivers_RiverId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Crops_CropId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_IrrigationSystems_IrrigationSystemId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Periods_PeriodId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_WaterSources_WaterSourceId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_ApplicationUsers_CapturePersonId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_ApplicationUsers_ValidationPersonId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_PropertyAddresses_PropertyAddressId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_GovernmentWaterControlAreas_Addresses_WaterControlAddressAddressId",
                table: "GovernmentWaterControlAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_IrrigationBoards_Addresses_IrrigationBoardAddressAddressId",
                table: "IrrigationBoards");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_LetterTypes_LetterTypeId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_PropertyOwners_PropertyOwnerOwnerId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Addresses_PropertyAddressAddressId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwners_Addresses_AddressId",
                table: "PropertyOwners");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwners_CustomerTypes_CustomerTypeId",
                table: "PropertyOwners");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwnerships_Properties_PropertyId",
                table: "PropertyOwnerships");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwnerships_PropertyOwners_PropertyOwnerId",
                table: "PropertyOwnerships");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Periods_PeriodId",
                table: "Storings");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Rivers_RiverOrStreamRiverId",
                table: "Storings");

            migrationBuilder.DropTable(
                name: "IssuedLetters");

            migrationBuilder.DropTable(
                name: "PropertyAddresses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Validations",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_PropertyAddressId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_ValidationPersonId",
                table: "FileMasters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "CustomerTitle",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "OwnerGender",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "PropertyNumber",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LetterDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "WaterSourceType",
                table: "Irrigations");

            migrationBuilder.DropColumn(
                name: "GetValidationStatus",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "LatestLetterTypeIssued",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "LegalTypeGroup",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "MyProperty",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "NameUpdate",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RegistrationStatusPostPublicParticipation",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RegistrationStatusPrePublicParticipation",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RegiteredForStoring",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RequirementDescription",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RiparianFarm",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "ValidationDescription",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "ValidationPersonId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "ValidationStartDate",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "SateliteQualifyPeriod",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "AuthorisationTypedescription",
                table: "AuthorisationTypes");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Validations",
                newName: "FileMasterId");

            migrationBuilder.RenameColumn(
                name: "WaterManagementArea",
                table: "Properties",
                newName: "QuaternaryDrainage");

            migrationBuilder.RenameColumn(
                name: "QuatenaryDrainage",
                table: "Properties",
                newName: "PropertyReferenceNumber");

            migrationBuilder.RenameColumn(
                name: "PropertyAddressAddressId",
                table: "Properties",
                newName: "WmaId");

            migrationBuilder.RenameIndex(
                name: "IX_Properties_PropertyAddressAddressId",
                table: "Properties",
                newName: "IX_Properties_WmaId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "LetterTypes",
                newName: "LetterTypeId");

            migrationBuilder.RenameColumn(
                name: "PropertyOwnerOwnerId",
                table: "LetterIssuances",
                newName: "FileMasterId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "LetterIssuances",
                newName: "LetterIssuanceId");

            migrationBuilder.RenameIndex(
                name: "IX_LetterIssuances_PropertyOwnerOwnerId",
                table: "LetterIssuances",
                newName: "IX_LetterIssuances_FileMasterId");

            migrationBuilder.RenameColumn(
                name: "WARMSPrintsReceived",
                table: "FileMasters",
                newName: "ValidationStatusName");

            migrationBuilder.RenameColumn(
                name: "PropertyAddressId",
                table: "FileMasters",
                newName: "PropertyId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "FileMasters",
                newName: "FileMasterId");

            migrationBuilder.AddColumn<Guid>(
                name: "ValidationId",
                table: "Validations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToId",
                table: "Validations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntitlementId",
                table: "Validations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "Validations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Validations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationDescription",
                table: "Validations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidationStartDate",
                table: "Validations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationStatusName",
                table: "Validations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FarmNumber",
                table: "SateliteImages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ImageSource",
                table: "SateliteImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "SateliteImages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "SateliteImages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityDocumentNumber",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "PropertyOwners",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerTypeId",
                table: "PropertyOwners",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "AddressId",
                table: "PropertyOwners",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "RegistrationDate",
                table: "Properties",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<decimal>(
                name: "PropertySize",
                table: "Properties",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ProclamationDate",
                table: "Properties",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NWASection",
                table: "LetterTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AgreedWithFindings",
                table: "LetterIssuances",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AvailableInPortal",
                table: "LetterIssuances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DigitalSignatureId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GeneratedDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueMethod",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IssuedDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PhysicalDeliveryDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PortalAcknowledgedByPublicUserId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PortalAcknowledgedDate",
                table: "LetterIssuances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PortalFirstViewedDate",
                table: "LetterIssuances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyOwnerId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReissuedFromId",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ResponseDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNotes",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseStatus",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReturnedToSender",
                table: "LetterIssuances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ServingOfficialName",
                table: "LetterIssuances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignedById",
                table: "LetterIssuances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SignedDate",
                table: "LetterIssuances",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileMasterId",
                table: "Irrigations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "Irrigations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WaterSourceId",
                table: "Irrigations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "Forestations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Forestations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "RegisteredForTakingWater",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "RegisteredForForestation",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CapturePersonId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntitlementId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RegisteredForStoring",
                table: "FileMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatorId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "FileMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Entitlements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntitlementTypeId",
                table: "Entitlements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Volume",
                table: "Entitlements",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DamCalculationStatus",
                table: "DamCalculations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SateliteImageId",
                table: "DamCalculations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AuthorisationTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationUserId",
                table: "ApplicationUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "ApplicationUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Validations",
                table: "Validations",
                column: "ValidationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers",
                column: "ApplicationUserId");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Authorisations",
                columns: table => new
                {
                    AuthorisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorisationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authorisations", x => x.AuthorisationId);
                    table.ForeignKey(
                        name: "FK_Authorisations_AuthorisationTypes_AuthorisationTypeId",
                        column: x => x.AuthorisationTypeId,
                        principalTable: "AuthorisationTypes",
                        principalColumn: "AuthorisationTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Authorisations_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ProvinceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvinceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ProvinceId);
                });

            migrationBuilder.CreateTable(
                name: "PublicUsers",
                columns: table => new
                {
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentityNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessRegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUsers", x => x.PublicUserId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStates",
                columns: table => new
                {
                    WorkflowStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStates", x => x.WorkflowStateId);
                });

            migrationBuilder.CreateTable(
                name: "WaterManagementAreas",
                columns: table => new
                {
                    WmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WmaName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WmaCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvinceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterManagementAreas", x => x.WmaId);
                    table.ForeignKey(
                        name: "FK_WaterManagementAreas_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseComments",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadByDWSDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadByPublicUserDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseComments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_CaseComments_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseComments_CaseComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "CaseComments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseComments_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseComments_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByPublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VirusScanStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentHash = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_Documents_ApplicationUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_PublicUsers_UploadedByPublicUserId",
                        column: x => x.UploadedByPublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    EmailSent = table.Column<bool>(type: "bit", nullable: false),
                    EmailSentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SmsSent = table.Column<bool>(type: "bit", nullable: false),
                    SmsSentDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Protests",
                columns: table => new
                {
                    ProtestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LodgedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "PublicUserProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUserProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicUserProperties_ApplicationUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicUserProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicUserProperties_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentWorkflowStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.WorkflowInstanceId);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_ApplicationUsers_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_FileMasters_FileMasterId",
                        column: x => x.FileMasterId,
                        principalTable: "FileMasters",
                        principalColumn: "FileMasterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowStates_CurrentWorkflowStateId",
                        column: x => x.CurrentWorkflowStateId,
                        principalTable: "WorkflowStates",
                        principalColumn: "WorkflowStateId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationalUnits",
                columns: table => new
                {
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvinceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentOrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterManagementAreaWmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationalUnits", x => x.OrgUnitId);
                    table.ForeignKey(
                        name: "FK_OrganisationalUnits_OrganisationalUnits_ParentOrgUnitId",
                        column: x => x.ParentOrgUnitId,
                        principalTable: "OrganisationalUnits",
                        principalColumn: "OrgUnitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationalUnits_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationalUnits_WaterManagementAreas_WaterManagementAreaWmaId",
                        column: x => x.WaterManagementAreaWmaId,
                        principalTable: "WaterManagementAreas",
                        principalColumn: "WmaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationalUnits_WaterManagementAreas_WmaId",
                        column: x => x.WmaId,
                        principalTable: "WaterManagementAreas",
                        principalColumn: "WmaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalSignatures",
                columns: table => new
                {
                    SignatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentHashAtSigning = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalSignatures", x => x.SignatureId);
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_PublicUsers_PublicUserId",
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
                    ProtestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "WorkflowStepRecords",
                columns: table => new
                {
                    WorkflowStepRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationErrors = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepRecords", x => x.WorkflowStepRecordId);
                    table.ForeignKey(
                        name: "FK_WorkflowStepRecords_ApplicationUsers_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStepRecords_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "WorkflowInstanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStepRecords_WorkflowStates_WorkflowStateId",
                        column: x => x.WorkflowStateId,
                        principalTable: "WorkflowStates",
                        principalColumn: "WorkflowStateId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SignatureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DigitalSignatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureRequests_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "ApplicationUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignatureRequests_DigitalSignatures_DigitalSignatureId",
                        column: x => x.DigitalSignatureId,
                        principalTable: "DigitalSignatures",
                        principalColumn: "SignatureId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignatureRequests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignatureRequests_PublicUsers_PublicUserId",
                        column: x => x.PublicUserId,
                        principalTable: "PublicUsers",
                        principalColumn: "PublicUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Validations_AssignedToId",
                table: "Validations",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_Validations_EntitlementId",
                table: "Validations",
                column: "EntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Validations_FileMasterId",
                table: "Validations",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Validations_PeriodId",
                table: "Validations",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Validations_PropertyId",
                table: "Validations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_SateliteImages_PeriodId",
                table: "SateliteImages",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_SateliteImages_PropertyId",
                table: "SateliteImages",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AddressId",
                table: "Properties",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_DigitalSignatureId",
                table: "LetterIssuances",
                column: "DigitalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_DocumentId",
                table: "LetterIssuances",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_PropertyOwnerId",
                table: "LetterIssuances",
                column: "PropertyOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_ReissuedFromId",
                table: "LetterIssuances",
                column: "ReissuedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_SignedById",
                table: "LetterIssuances",
                column: "SignedById");

            migrationBuilder.CreateIndex(
                name: "IX_Irrigations_FileMasterId",
                table: "Irrigations",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Irrigations_PeriodId",
                table: "Irrigations",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Irrigations_WaterSourceId",
                table: "Irrigations",
                column: "WaterSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Forestations_PeriodId",
                table: "Forestations",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Forestations_PropertyId",
                table: "Forestations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_EntitlementId",
                table: "FileMasters",
                column: "EntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_OrgUnitId",
                table: "FileMasters",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_PropertyId",
                table: "FileMasters",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_ValidatorId",
                table: "FileMasters",
                column: "ValidatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_EntitlementTypeId",
                table: "Entitlements",
                column: "EntitlementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DamCalculations_SateliteImageId",
                table: "DamCalculations",
                column: "SateliteImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers",
                column: "OrganisationalUnitOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_OrgUnitId",
                table: "ApplicationUsers",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ApplicationUserId",
                table: "AuditLogs",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorisations_AuthorisationTypeId",
                table: "Authorisations",
                column: "AuthorisationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorisations_FileMasterId",
                table: "Authorisations",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_ApplicationUserId",
                table: "CaseComments",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_FileMasterId",
                table: "CaseComments",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_ParentCommentId",
                table: "CaseComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_PublicUserId",
                table: "CaseComments",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_ApplicationUserId",
                table: "DigitalSignatures",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_DocumentId",
                table: "DigitalSignatures",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_PublicUserId",
                table: "DigitalSignatures",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileMasterId",
                table: "Documents",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByPublicUserId",
                table: "Documents",
                column: "UploadedByPublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ApplicationUserId",
                table: "Notifications",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_FileMasterId",
                table: "Notifications",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PublicUserId",
                table: "Notifications",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationalUnits_ParentOrgUnitId",
                table: "OrganisationalUnits",
                column: "ParentOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationalUnits_ProvinceId",
                table: "OrganisationalUnits",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationalUnits_WaterManagementAreaWmaId",
                table: "OrganisationalUnits",
                column: "WaterManagementAreaWmaId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationalUnits_WmaId",
                table: "OrganisationalUnits",
                column: "WmaId");

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

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_ApprovedByUserId",
                table: "PublicUserProperties",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_PropertyId",
                table: "PublicUserProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUserProperties_PublicUserId",
                table: "PublicUserProperties",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureRequests_ApplicationUserId",
                table: "SignatureRequests",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureRequests_DigitalSignatureId",
                table: "SignatureRequests",
                column: "DigitalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureRequests_DocumentId",
                table: "SignatureRequests",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureRequests_PublicUserId",
                table: "SignatureRequests",
                column: "PublicUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterManagementAreas_ProvinceId",
                table: "WaterManagementAreas",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_AssignedToId",
                table: "WorkflowInstances",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentWorkflowStateId",
                table: "WorkflowInstances",
                column: "CurrentWorkflowStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_FileMasterId",
                table: "WorkflowInstances",
                column: "FileMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRecords_CompletedById",
                table: "WorkflowStepRecords",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRecords_WorkflowInstanceId",
                table: "WorkflowStepRecords",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepRecords_WorkflowStateId",
                table: "WorkflowStepRecords",
                column: "WorkflowStateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUsers_OrganisationalUnits_OrgUnitId",
                table: "ApplicationUsers",
                column: "OrgUnitId",
                principalTable: "OrganisationalUnits",
                principalColumn: "OrgUnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUsers_OrganisationalUnits_OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers",
                column: "OrganisationalUnitOrgUnitId",
                principalTable: "OrganisationalUnits",
                principalColumn: "OrgUnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crops_CropTypes_CropTypeId",
                table: "Crops",
                column: "CropTypeId",
                principalTable: "CropTypes",
                principalColumn: "CropTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Rivers_RiverId",
                table: "DamCalculations",
                column: "RiverId",
                principalTable: "Rivers",
                principalColumn: "RiverId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_SateliteImages_SateliteImageId",
                table: "DamCalculations",
                column: "SateliteImageId",
                principalTable: "SateliteImages",
                principalColumn: "ImageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Entitlements_EntitlementTypes_EntitlementTypeId",
                table: "Entitlements",
                column: "EntitlementTypeId",
                principalTable: "EntitlementTypes",
                principalColumn: "EntitlementTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Crops_CropId",
                table: "FieldAndCrops",
                column: "CropId",
                principalTable: "Crops",
                principalColumn: "CropId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_IrrigationSystems_IrrigationSystemId",
                table: "FieldAndCrops",
                column: "IrrigationSystemId",
                principalTable: "IrrigationSystems",
                principalColumn: "IrrigationSystemId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Periods_PeriodId",
                table: "FieldAndCrops",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_WaterSources_WaterSourceId",
                table: "FieldAndCrops",
                column: "WaterSourceId",
                principalTable: "WaterSources",
                principalColumn: "WaterSourceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_ApplicationUsers_CapturePersonId",
                table: "FileMasters",
                column: "CapturePersonId",
                principalTable: "ApplicationUsers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_ApplicationUsers_ValidatorId",
                table: "FileMasters",
                column: "ValidatorId",
                principalTable: "ApplicationUsers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_Entitlements_EntitlementId",
                table: "FileMasters",
                column: "EntitlementId",
                principalTable: "Entitlements",
                principalColumn: "EntitlementId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_OrganisationalUnits_OrgUnitId",
                table: "FileMasters",
                column: "OrgUnitId",
                principalTable: "OrganisationalUnits",
                principalColumn: "OrgUnitId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_Properties_PropertyId",
                table: "FileMasters",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Forestations_Periods_PeriodId",
                table: "Forestations",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GovernmentWaterControlAreas_Addresses_WaterControlAddressAddressId",
                table: "GovernmentWaterControlAreas",
                column: "WaterControlAddressAddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IrrigationBoards_Addresses_IrrigationBoardAddressAddressId",
                table: "IrrigationBoards",
                column: "IrrigationBoardAddressAddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_FileMasters_FileMasterId",
                table: "Irrigations",
                column: "FileMasterId",
                principalTable: "FileMasters",
                principalColumn: "FileMasterId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_Periods_PeriodId",
                table: "Irrigations",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_WaterSources_WaterSourceId",
                table: "Irrigations",
                column: "WaterSourceId",
                principalTable: "WaterSources",
                principalColumn: "WaterSourceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_ApplicationUsers_SignedById",
                table: "LetterIssuances",
                column: "SignedById",
                principalTable: "ApplicationUsers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_DigitalSignatures_DigitalSignatureId",
                table: "LetterIssuances",
                column: "DigitalSignatureId",
                principalTable: "DigitalSignatures",
                principalColumn: "SignatureId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_Documents_DocumentId",
                table: "LetterIssuances",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "DocumentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_FileMasters_FileMasterId",
                table: "LetterIssuances",
                column: "FileMasterId",
                principalTable: "FileMasters",
                principalColumn: "FileMasterId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_LetterIssuances_ReissuedFromId",
                table: "LetterIssuances",
                column: "ReissuedFromId",
                principalTable: "LetterIssuances",
                principalColumn: "LetterIssuanceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_LetterTypes_LetterTypeId",
                table: "LetterIssuances",
                column: "LetterTypeId",
                principalTable: "LetterTypes",
                principalColumn: "LetterTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_PropertyOwners_PropertyOwnerId",
                table: "LetterIssuances",
                column: "PropertyOwnerId",
                principalTable: "PropertyOwners",
                principalColumn: "OwnerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Addresses_AddressId",
                table: "Properties",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_WaterManagementAreas_WmaId",
                table: "Properties",
                column: "WmaId",
                principalTable: "WaterManagementAreas",
                principalColumn: "WmaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwners_Addresses_AddressId",
                table: "PropertyOwners",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwners_CustomerTypes_CustomerTypeId",
                table: "PropertyOwners",
                column: "CustomerTypeId",
                principalTable: "CustomerTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwnerships_Properties_PropertyId",
                table: "PropertyOwnerships",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwnerships_PropertyOwners_PropertyOwnerId",
                table: "PropertyOwnerships",
                column: "PropertyOwnerId",
                principalTable: "PropertyOwners",
                principalColumn: "OwnerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SateliteImages_Periods_PeriodId",
                table: "SateliteImages",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SateliteImages_Properties_PropertyId",
                table: "SateliteImages",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Periods_PeriodId",
                table: "Storings",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Rivers_RiverOrStreamRiverId",
                table: "Storings",
                column: "RiverOrStreamRiverId",
                principalTable: "Rivers",
                principalColumn: "RiverId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Validations_ApplicationUsers_AssignedToId",
                table: "Validations",
                column: "AssignedToId",
                principalTable: "ApplicationUsers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Validations_Entitlements_EntitlementId",
                table: "Validations",
                column: "EntitlementId",
                principalTable: "Entitlements",
                principalColumn: "EntitlementId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Validations_FileMasters_FileMasterId",
                table: "Validations",
                column: "FileMasterId",
                principalTable: "FileMasters",
                principalColumn: "FileMasterId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Validations_Periods_PeriodId",
                table: "Validations",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Validations_Properties_PropertyId",
                table: "Validations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationUsers_OrganisationalUnits_OrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationUsers_OrganisationalUnits_OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Crops_CropTypes_CropTypeId",
                table: "Crops");

            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_Rivers_RiverId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_DamCalculations_SateliteImages_SateliteImageId",
                table: "DamCalculations");

            migrationBuilder.DropForeignKey(
                name: "FK_Entitlements_EntitlementTypes_EntitlementTypeId",
                table: "Entitlements");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Crops_CropId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_IrrigationSystems_IrrigationSystemId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Periods_PeriodId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldAndCrops_WaterSources_WaterSourceId",
                table: "FieldAndCrops");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_ApplicationUsers_CapturePersonId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_ApplicationUsers_ValidatorId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_Entitlements_EntitlementId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_OrganisationalUnits_OrgUnitId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_FileMasters_Properties_PropertyId",
                table: "FileMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_Forestations_Periods_PeriodId",
                table: "Forestations");

            migrationBuilder.DropForeignKey(
                name: "FK_Forestations_Properties_PropertyId",
                table: "Forestations");

            migrationBuilder.DropForeignKey(
                name: "FK_GovernmentWaterControlAreas_Addresses_WaterControlAddressAddressId",
                table: "GovernmentWaterControlAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_IrrigationBoards_Addresses_IrrigationBoardAddressAddressId",
                table: "IrrigationBoards");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_FileMasters_FileMasterId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_Periods_PeriodId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_Irrigations_WaterSources_WaterSourceId",
                table: "Irrigations");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_ApplicationUsers_SignedById",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_DigitalSignatures_DigitalSignatureId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_Documents_DocumentId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_FileMasters_FileMasterId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_LetterIssuances_ReissuedFromId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_LetterTypes_LetterTypeId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterIssuances_PropertyOwners_PropertyOwnerId",
                table: "LetterIssuances");

            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Addresses_AddressId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_Properties_WaterManagementAreas_WmaId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwners_Addresses_AddressId",
                table: "PropertyOwners");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwners_CustomerTypes_CustomerTypeId",
                table: "PropertyOwners");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwnerships_Properties_PropertyId",
                table: "PropertyOwnerships");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyOwnerships_PropertyOwners_PropertyOwnerId",
                table: "PropertyOwnerships");

            migrationBuilder.DropForeignKey(
                name: "FK_SateliteImages_Periods_PeriodId",
                table: "SateliteImages");

            migrationBuilder.DropForeignKey(
                name: "FK_SateliteImages_Properties_PropertyId",
                table: "SateliteImages");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Periods_PeriodId",
                table: "Storings");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings");

            migrationBuilder.DropForeignKey(
                name: "FK_Storings_Rivers_RiverOrStreamRiverId",
                table: "Storings");

            migrationBuilder.DropForeignKey(
                name: "FK_Validations_ApplicationUsers_AssignedToId",
                table: "Validations");

            migrationBuilder.DropForeignKey(
                name: "FK_Validations_Entitlements_EntitlementId",
                table: "Validations");

            migrationBuilder.DropForeignKey(
                name: "FK_Validations_FileMasters_FileMasterId",
                table: "Validations");

            migrationBuilder.DropForeignKey(
                name: "FK_Validations_Periods_PeriodId",
                table: "Validations");

            migrationBuilder.DropForeignKey(
                name: "FK_Validations_Properties_PropertyId",
                table: "Validations");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Authorisations");

            migrationBuilder.DropTable(
                name: "CaseComments");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OrganisationalUnits");

            migrationBuilder.DropTable(
                name: "ProtestDocuments");

            migrationBuilder.DropTable(
                name: "PublicUserProperties");

            migrationBuilder.DropTable(
                name: "SignatureRequests");

            migrationBuilder.DropTable(
                name: "WorkflowStepRecords");

            migrationBuilder.DropTable(
                name: "WaterManagementAreas");

            migrationBuilder.DropTable(
                name: "Protests");

            migrationBuilder.DropTable(
                name: "DigitalSignatures");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "WorkflowStates");

            migrationBuilder.DropTable(
                name: "PublicUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Validations",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_Validations_AssignedToId",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_Validations_EntitlementId",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_Validations_FileMasterId",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_Validations_PeriodId",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_Validations_PropertyId",
                table: "Validations");

            migrationBuilder.DropIndex(
                name: "IX_SateliteImages_PeriodId",
                table: "SateliteImages");

            migrationBuilder.DropIndex(
                name: "IX_SateliteImages_PropertyId",
                table: "SateliteImages");

            migrationBuilder.DropIndex(
                name: "IX_Properties_AddressId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_DigitalSignatureId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_DocumentId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_PropertyOwnerId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_ReissuedFromId",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_LetterIssuances_SignedById",
                table: "LetterIssuances");

            migrationBuilder.DropIndex(
                name: "IX_Irrigations_FileMasterId",
                table: "Irrigations");

            migrationBuilder.DropIndex(
                name: "IX_Irrigations_PeriodId",
                table: "Irrigations");

            migrationBuilder.DropIndex(
                name: "IX_Irrigations_WaterSourceId",
                table: "Irrigations");

            migrationBuilder.DropIndex(
                name: "IX_Forestations_PeriodId",
                table: "Forestations");

            migrationBuilder.DropIndex(
                name: "IX_Forestations_PropertyId",
                table: "Forestations");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_EntitlementId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_OrgUnitId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_PropertyId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_FileMasters_ValidatorId",
                table: "FileMasters");

            migrationBuilder.DropIndex(
                name: "IX_Entitlements_EntitlementTypeId",
                table: "Entitlements");

            migrationBuilder.DropIndex(
                name: "IX_DamCalculations_SateliteImageId",
                table: "DamCalculations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_OrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "ValidationId",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "EntitlementId",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "ValidationDescription",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "ValidationStartDate",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "ValidationStatusName",
                table: "Validations");

            migrationBuilder.DropColumn(
                name: "ImageSource",
                table: "SateliteImages");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "SateliteImages");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "SateliteImages");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PropertyOwners");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "NWASection",
                table: "LetterTypes");

            migrationBuilder.DropColumn(
                name: "AgreedWithFindings",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "AvailableInPortal",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "DigitalSignatureId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "GeneratedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "IssueMethod",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "IssuedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PhysicalDeliveryDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PortalAcknowledgedByPublicUserId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PortalAcknowledgedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PortalFirstViewedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "PropertyOwnerId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ReissuedFromId",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ResponseDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ResponseNotes",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ResponseStatus",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ReturnedToSender",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "ServingOfficialName",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "SignedById",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "SignedDate",
                table: "LetterIssuances");

            migrationBuilder.DropColumn(
                name: "FileMasterId",
                table: "Irrigations");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "Irrigations");

            migrationBuilder.DropColumn(
                name: "WaterSourceId",
                table: "Irrigations");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "Forestations");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Forestations");

            migrationBuilder.DropColumn(
                name: "EntitlementId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "RegisteredForStoring",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "ValidatorId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "FileMasters");

            migrationBuilder.DropColumn(
                name: "EntitlementTypeId",
                table: "Entitlements");

            migrationBuilder.DropColumn(
                name: "Volume",
                table: "Entitlements");

            migrationBuilder.DropColumn(
                name: "DamCalculationStatus",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "SateliteImageId",
                table: "DamCalculations");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AuthorisationTypes");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "OrganisationalUnitOrgUnitId",
                table: "ApplicationUsers");

            migrationBuilder.RenameColumn(
                name: "FileMasterId",
                table: "Validations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "WmaId",
                table: "Properties",
                newName: "PropertyAddressAddressId");

            migrationBuilder.RenameColumn(
                name: "QuaternaryDrainage",
                table: "Properties",
                newName: "WaterManagementArea");

            migrationBuilder.RenameColumn(
                name: "PropertyReferenceNumber",
                table: "Properties",
                newName: "QuatenaryDrainage");

            migrationBuilder.RenameIndex(
                name: "IX_Properties_WmaId",
                table: "Properties",
                newName: "IX_Properties_PropertyAddressAddressId");

            migrationBuilder.RenameColumn(
                name: "LetterTypeId",
                table: "LetterTypes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "FileMasterId",
                table: "LetterIssuances",
                newName: "PropertyOwnerOwnerId");

            migrationBuilder.RenameColumn(
                name: "LetterIssuanceId",
                table: "LetterIssuances",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_LetterIssuances_FileMasterId",
                table: "LetterIssuances",
                newName: "IX_LetterIssuances_PropertyOwnerOwnerId");

            migrationBuilder.RenameColumn(
                name: "ValidationStatusName",
                table: "FileMasters",
                newName: "WARMSPrintsReceived");

            migrationBuilder.RenameColumn(
                name: "PropertyId",
                table: "FileMasters",
                newName: "PropertyAddressId");

            migrationBuilder.RenameColumn(
                name: "FileMasterId",
                table: "FileMasters",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "FarmNumber",
                table: "SateliteImages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IdentityDocumentNumber",
                table: "PropertyOwners",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "PropertyOwners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "PropertyOwners",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerTypeId",
                table: "PropertyOwners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AddressId",
                table: "PropertyOwners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerTitle",
                table: "PropertyOwners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OwnerGender",
                table: "PropertyOwners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "RegistrationDate",
                table: "Properties",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PropertySize",
                table: "Properties",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ProclamationDate",
                table: "Properties",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyNumber",
                table: "Properties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LetterDate",
                table: "LetterIssuances",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "WaterSourceType",
                table: "Irrigations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "RegisteredForTakingWater",
                table: "FileMasters",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "RegisteredForForestation",
                table: "FileMasters",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "CapturePersonId",
                table: "FileMasters",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GetValidationStatus",
                table: "FileMasters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestLetterTypeIssued",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "FileMasters",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalTypeGroup",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "FileMasters",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MyProperty",
                table: "FileMasters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameUpdate",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationStatusPostPublicParticipation",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationStatusPrePublicParticipation",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RegiteredForStoring",
                table: "FileMasters",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequirementDescription",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiparianFarm",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationDescription",
                table: "FileMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationPersonId",
                table: "FileMasters",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidationStartDate",
                table: "FileMasters",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Entitlements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "SateliteQualifyPeriod",
                table: "DamCalculations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthorisationTypedescription",
                table: "AuthorisationTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "ApplicationUsers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Validations",
                table: "Validations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "IssuedLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address4 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalAddress1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalAddress2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalAddress3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyReference = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAddresses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_PropertyAddressId",
                table: "FileMasters",
                column: "PropertyAddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_ValidationPersonId",
                table: "FileMasters",
                column: "ValidationPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Crops_CropTypes_CropTypeId",
                table: "Crops",
                column: "CropTypeId",
                principalTable: "CropTypes",
                principalColumn: "CropTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Properties_PropertyId",
                table: "DamCalculations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DamCalculations_Rivers_RiverId",
                table: "DamCalculations",
                column: "RiverId",
                principalTable: "Rivers",
                principalColumn: "RiverId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Crops_CropId",
                table: "FieldAndCrops",
                column: "CropId",
                principalTable: "Crops",
                principalColumn: "CropId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_IrrigationSystems_IrrigationSystemId",
                table: "FieldAndCrops",
                column: "IrrigationSystemId",
                principalTable: "IrrigationSystems",
                principalColumn: "IrrigationSystemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Periods_PeriodId",
                table: "FieldAndCrops",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_Properties_PropertyId",
                table: "FieldAndCrops",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAndCrops_WaterSources_WaterSourceId",
                table: "FieldAndCrops",
                column: "WaterSourceId",
                principalTable: "WaterSources",
                principalColumn: "WaterSourceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_ApplicationUsers_CapturePersonId",
                table: "FileMasters",
                column: "CapturePersonId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_ApplicationUsers_ValidationPersonId",
                table: "FileMasters",
                column: "ValidationPersonId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileMasters_PropertyAddresses_PropertyAddressId",
                table: "FileMasters",
                column: "PropertyAddressId",
                principalTable: "PropertyAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GovernmentWaterControlAreas_Addresses_WaterControlAddressAddressId",
                table: "GovernmentWaterControlAreas",
                column: "WaterControlAddressAddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_IrrigationBoards_Addresses_IrrigationBoardAddressAddressId",
                table: "IrrigationBoards",
                column: "IrrigationBoardAddressAddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Irrigations_Properties_PropertyId",
                table: "Irrigations",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_LetterTypes_LetterTypeId",
                table: "LetterIssuances",
                column: "LetterTypeId",
                principalTable: "LetterTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterIssuances_PropertyOwners_PropertyOwnerOwnerId",
                table: "LetterIssuances",
                column: "PropertyOwnerOwnerId",
                principalTable: "PropertyOwners",
                principalColumn: "OwnerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Addresses_PropertyAddressAddressId",
                table: "Properties",
                column: "PropertyAddressAddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwners_Addresses_AddressId",
                table: "PropertyOwners",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "AddressId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwners_CustomerTypes_CustomerTypeId",
                table: "PropertyOwners",
                column: "CustomerTypeId",
                principalTable: "CustomerTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwnerships_Properties_PropertyId",
                table: "PropertyOwnerships",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyOwnerships_PropertyOwners_PropertyOwnerId",
                table: "PropertyOwnerships",
                column: "PropertyOwnerId",
                principalTable: "PropertyOwners",
                principalColumn: "OwnerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Periods_PeriodId",
                table: "Storings",
                column: "PeriodId",
                principalTable: "Periods",
                principalColumn: "PeriodId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Properties_PropertyId",
                table: "Storings",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "PropertyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Storings_Rivers_RiverOrStreamRiverId",
                table: "Storings",
                column: "RiverOrStreamRiverId",
                principalTable: "Rivers",
                principalColumn: "RiverId");
        }
    }
}
