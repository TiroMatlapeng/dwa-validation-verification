using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dwa_ver_val.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuburbName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressId);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthorisationTypes",
                columns: table => new
                {
                    AuthorisationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorisationTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorisationTypedescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorisationTypes", x => x.AuthorisationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "CropTypes",
                columns: table => new
                {
                    CropTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CropTypeDecription = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropTypes", x => x.CropTypeId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entitlements",
                columns: table => new
                {
                    EntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entitlements", x => x.EntitlementId);
                });

            migrationBuilder.CreateTable(
                name: "EntitlementTypes",
                columns: table => new
                {
                    EntitlementTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntitlementName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntitlementDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntitlementTypes", x => x.EntitlementTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Forestations",
                columns: table => new
                {
                    ForestationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WithinGWCA = table.Column<bool>(type: "bit", nullable: true),
                    QualifyPeriodSFRAHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrentPeriodSFRAHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QualifyPeriodVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Specie = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaterResource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisteredHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RegisteredVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UserFeedbackEntitlementType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserFeedbackEntitlementReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserFeedbackEntitlementHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CommentOnFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ELUHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ELUVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnlawfulHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnlawfulVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LawfulHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LawfulVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitForVolumeCalculation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pre1972Hectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Pre1972Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SFRAPermitNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SFRAPermitHectares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommentsOnData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forestations", x => x.ForestationId);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentWaterSchemes",
                columns: table => new
                {
                    WaterSchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterSchemeName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentWaterSchemes", x => x.WaterSchemeId);
                });

            migrationBuilder.CreateTable(
                name: "IrrigationSystems",
                columns: table => new
                {
                    IrrigationSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationSystemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IrrigationSystemDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IrrigationSystemModel = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationSystems", x => x.IrrigationSystemId);
                });

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
                name: "LetterTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LetterDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Periods",
                columns: table => new
                {
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periods", x => x.PeriodId);
                });

            migrationBuilder.CreateTable(
                name: "PropertyAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address4 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalAddress1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalAddress2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalAddress3 = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAddresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rivers",
                columns: table => new
                {
                    RiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiverName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rivers", x => x.RiverId);
                });

            migrationBuilder.CreateTable(
                name: "SateliteImages",
                columns: table => new
                {
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FarmNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MapCompilation = table.Column<DateOnly>(type: "date", nullable: true),
                    ImageDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ImageNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SateliteImages", x => x.ImageId);
                });

            migrationBuilder.CreateTable(
                name: "Validations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterSources",
                columns: table => new
                {
                    WaterSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterSourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WaterSourceType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSources", x => x.WaterSourceId);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentWaterControlAreas",
                columns: table => new
                {
                    WaterControlAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernmentWaterControlAreaName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WaterControlAddressAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterControlPhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentWaterControlAreas", x => x.WaterControlAreaId);
                    table.ForeignKey(
                        name: "FK_GovernmentWaterControlAreas_Addresses_WaterControlAddressAddressId",
                        column: x => x.WaterControlAddressAddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressId");
                });

            migrationBuilder.CreateTable(
                name: "IrrigationBoards",
                columns: table => new
                {
                    IrrigationBoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationBoardName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IrrigationBoardPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IrrigationBoardAddressAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationBoards", x => x.IrrigationBoardId);
                    table.ForeignKey(
                        name: "FK_IrrigationBoards_Addresses_IrrigationBoardAddressAddressId",
                        column: x => x.IrrigationBoardAddressAddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressId");
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyAddressAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertySize = table.Column<int>(type: "int", nullable: false),
                    ProclamationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistrationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SGCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuatenaryDrainage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaterManagementArea = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.PropertyId);
                    table.ForeignKey(
                        name: "FK_Properties_Addresses_PropertyAddressAddressId",
                        column: x => x.PropertyAddressAddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressId");
                });

            migrationBuilder.CreateTable(
                name: "Crops",
                columns: table => new
                {
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.CropId);
                    table.ForeignKey(
                        name: "FK_Crops_CropTypes_CropTypeId",
                        column: x => x.CropTypeId,
                        principalTable: "CropTypes",
                        principalColumn: "CropTypeId");
                });

            migrationBuilder.CreateTable(
                name: "PropertyOwners",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    CustomerTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerTitle = table.Column<int>(type: "int", nullable: false),
                    IdentityDocumentNumber = table.Column<int>(type: "int", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerGender = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyOwners", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_PropertyOwners_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyOwners_CustomerTypes_CustomerTypeId",
                        column: x => x.CustomerTypeId,
                        principalTable: "CustomerTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyorGeneralCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryCatchment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuaternaryCatchment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FarmName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FarmNumber = table.Column<int>(type: "int", nullable: false),
                    RegistrationDivision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FarmPortion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    NameUpdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyIndex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrationStatusPrePublicParticipation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrationStatusPostPublicParticipation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarmsApplicant = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileCreatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FileStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequirementDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WARMSPrintsReceived = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Group = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalTypeGroup = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiparianFarm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisteredForTakingWater = table.Column<bool>(type: "bit", nullable: true),
                    RegiteredForStoring = table.Column<bool>(type: "bit", nullable: true),
                    RegisteredForForestation = table.Column<bool>(type: "bit", nullable: true),
                    BatchDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LatestLetterTypeIssued = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GetValidationStatus = table.Column<int>(type: "int", nullable: false),
                    ValidationPersonId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ValidationStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidationDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CapturePersonId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MyProperty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileMasters_ApplicationUsers_CapturePersonId",
                        column: x => x.CapturePersonId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FileMasters_ApplicationUsers_ValidationPersonId",
                        column: x => x.ValidationPersonId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileMasters_PropertyAddresses_PropertyAddressId",
                        column: x => x.PropertyAddressId,
                        principalTable: "PropertyAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DamCalculations",
                columns: table => new
                {
                    DamCalculationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SateliteQualifyPeriod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SateliteSurveyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DamNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DamCapacity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamCalculations", x => x.DamCalculationId);
                    table.ForeignKey(
                        name: "FK_DamCalculations_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DamCalculations_Rivers_RiverId",
                        column: x => x.RiverId,
                        principalTable: "Rivers",
                        principalColumn: "RiverId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Irrigations",
                columns: table => new
                {
                    IrrigationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IrrigationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaterDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WaterVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterLandArea = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterCropArea = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterSourceType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Irrigations", x => x.IrrigationId);
                    table.ForeignKey(
                        name: "FK_Irrigations_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Storings",
                columns: table => new
                {
                    StoringId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumberOfDams = table.Column<int>(type: "int", nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiverOrStreamRiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifactionScenario = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storings", x => x.StoringId);
                    table.ForeignKey(
                        name: "FK_Storings_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "Periods",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Storings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Storings_Rivers_RiverOrStreamRiverId",
                        column: x => x.RiverOrStreamRiverId,
                        principalTable: "Rivers",
                        principalColumn: "RiverId");
                });

            migrationBuilder.CreateTable(
                name: "FieldAndCrops",
                columns: table => new
                {
                    FieldAndCropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FieldArea = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlantDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RotationFactor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IrrigationSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropArea = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SAPWATCalculationResult = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldAndCrops", x => x.FieldAndCropId);
                    table.ForeignKey(
                        name: "FK_FieldAndCrops_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "CropId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldAndCrops_IrrigationSystems_IrrigationSystemId",
                        column: x => x.IrrigationSystemId,
                        principalTable: "IrrigationSystems",
                        principalColumn: "IrrigationSystemId");
                    table.ForeignKey(
                        name: "FK_FieldAndCrops_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "Periods",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldAndCrops_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldAndCrops_WaterSources_WaterSourceId",
                        column: x => x.WaterSourceId,
                        principalTable: "WaterSources",
                        principalColumn: "WaterSourceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LetterIssuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyOwnerOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterIssuances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterIssuances_LetterTypes_LetterTypeId",
                        column: x => x.LetterTypeId,
                        principalTable: "LetterTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LetterIssuances_PropertyOwners_PropertyOwnerOwnerId",
                        column: x => x.PropertyOwnerOwnerId,
                        principalTable: "PropertyOwners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyOwnerships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleDeedNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleDeedDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyOwnerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyOwnerships_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyOwnerships_PropertyOwners_PropertyOwnerId",
                        column: x => x.PropertyOwnerId,
                        principalTable: "PropertyOwners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crops_CropTypeId",
                table: "Crops",
                column: "CropTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DamCalculations_PropertyId",
                table: "DamCalculations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_DamCalculations_RiverId",
                table: "DamCalculations",
                column: "RiverId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAndCrops_CropId",
                table: "FieldAndCrops",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAndCrops_IrrigationSystemId",
                table: "FieldAndCrops",
                column: "IrrigationSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAndCrops_PeriodId",
                table: "FieldAndCrops",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAndCrops_PropertyId",
                table: "FieldAndCrops",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAndCrops_WaterSourceId",
                table: "FieldAndCrops",
                column: "WaterSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_CapturePersonId",
                table: "FileMasters",
                column: "CapturePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_PropertyAddressId",
                table: "FileMasters",
                column: "PropertyAddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_ValidationPersonId",
                table: "FileMasters",
                column: "ValidationPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentWaterControlAreas_WaterControlAddressAddressId",
                table: "GovernmentWaterControlAreas",
                column: "WaterControlAddressAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationBoards_IrrigationBoardAddressAddressId",
                table: "IrrigationBoards",
                column: "IrrigationBoardAddressAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Irrigations_PropertyId",
                table: "Irrigations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_LetterTypeId",
                table: "LetterIssuances",
                column: "LetterTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterIssuances_PropertyOwnerOwnerId",
                table: "LetterIssuances",
                column: "PropertyOwnerOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_PropertyAddressAddressId",
                table: "Properties",
                column: "PropertyAddressAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyOwners_AddressId",
                table: "PropertyOwners",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyOwners_CustomerTypeId",
                table: "PropertyOwners",
                column: "CustomerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyOwnerships_PropertyId",
                table: "PropertyOwnerships",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyOwnerships_PropertyOwnerId",
                table: "PropertyOwnerships",
                column: "PropertyOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Storings_PeriodId",
                table: "Storings",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Storings_PropertyId",
                table: "Storings",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Storings_RiverOrStreamRiverId",
                table: "Storings",
                column: "RiverOrStreamRiverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthorisationTypes");

            migrationBuilder.DropTable(
                name: "DamCalculations");

            migrationBuilder.DropTable(
                name: "Entitlements");

            migrationBuilder.DropTable(
                name: "EntitlementTypes");

            migrationBuilder.DropTable(
                name: "FieldAndCrops");

            migrationBuilder.DropTable(
                name: "FileMasters");

            migrationBuilder.DropTable(
                name: "Forestations");

            migrationBuilder.DropTable(
                name: "GovernmentWaterControlAreas");

            migrationBuilder.DropTable(
                name: "GovernmentWaterSchemes");

            migrationBuilder.DropTable(
                name: "IrrigationBoards");

            migrationBuilder.DropTable(
                name: "Irrigations");

            migrationBuilder.DropTable(
                name: "IssuedLetters");

            migrationBuilder.DropTable(
                name: "LetterIssuances");

            migrationBuilder.DropTable(
                name: "PropertyOwnerships");

            migrationBuilder.DropTable(
                name: "SateliteImages");

            migrationBuilder.DropTable(
                name: "Storings");

            migrationBuilder.DropTable(
                name: "Validations");

            migrationBuilder.DropTable(
                name: "Crops");

            migrationBuilder.DropTable(
                name: "IrrigationSystems");

            migrationBuilder.DropTable(
                name: "WaterSources");

            migrationBuilder.DropTable(
                name: "ApplicationUsers");

            migrationBuilder.DropTable(
                name: "PropertyAddresses");

            migrationBuilder.DropTable(
                name: "LetterTypes");

            migrationBuilder.DropTable(
                name: "PropertyOwners");

            migrationBuilder.DropTable(
                name: "Periods");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "Rivers");

            migrationBuilder.DropTable(
                name: "CropTypes");

            migrationBuilder.DropTable(
                name: "CustomerTypes");

            migrationBuilder.DropTable(
                name: "Addresses");
        }
    }
}
