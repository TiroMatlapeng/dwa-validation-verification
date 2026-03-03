# DWA Validation & Verification System - Detailed Data Flow and Workflow Design

**Version:** 1.0
**Date:** 17 February 2026
**Status:** Draft for Review
**Author:** Senior Architect / Edwin Matlapeng

---

## Table of Contents

1. [System Overview & Context](#1-system-overview--context)
2. [System Architecture](#2-system-architecture)
3. [User Roles & Access Control](#3-user-roles--access-control)
4. [Core Domain Model Relationships](#4-core-domain-model-relationships)
5. [V&V Workflow Engine - Phase 1: Project Inception](#5-vv-workflow-engine---phase-1-project-inception)
6. [V&V Workflow Engine - Phase 2: Verification Process](#6-vv-workflow-engine---phase-2-verification-process)
7. [V&V Workflow Engine - Phase 3: Validation (Section 35) Process](#7-vv-workflow-engine---phase-3-validation-section-35-process)
8. [Calculator Engine Design](#8-calculator-engine-design)
9. [Letter Generation & Correspondence Engine](#9-letter-generation--correspondence-engine)
10. [Notification Engine](#10-notification-engine)
11. [Electronic Signature Module](#11-electronic-signature-module)
12. [Public Portal - Water User Self-Service](#12-public-portal---water-user-self-service)
13. [External System Integration](#13-external-system-integration)
14. [Reporting & Audit Trail](#14-reporting--audit-trail)
15. [Data Flow Diagrams](#15-data-flow-diagrams)
16. [Component Inventory & Build Status](#16-component-inventory--build-status)
17. [Database Design Considerations](#17-database-design-considerations)
18. [Implementation Roadmap](#18-implementation-roadmap)

---

## 1. System Overview & Context

### 1.1 Purpose

The DWA V&V System is an electronic information management system for validation and verification of existing lawful water use (ELU) and compulsory licencing nationally, as mandated by sections 32-35 of the National Water Act (NWA). The system will manage approximately 500,000 property records, with V&V to be completed for approximately 125,000 properties.

### 1.2 Business Drivers

- Digitise the V&V process currently done via spreadsheets and MS Access databases at regional level
- Automate workflow to track every property through the V&V pipeline
- Enable public water users to track their cases via a self-service portal
- Integrate with WARMS and eWULAAS (both on C#/ASP.NET/SQL Server)
- Generate Section 35 letters, manage correspondence, and capture electronic signatures
- Provide audit trails and progress reporting per catchment

### 1.3 Technology Stack

| Layer | Technology |
|-------|-----------|
| Backend Framework | ASP.NET Core 10.0 (MVC + Web API) |
| Database | SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 10.0 |
| Frontend (Internal) | Razor Views + Bootstrap + Chart.js |
| Frontend (Public Portal) | ASP.NET Core Razor Pages or Blazor (separate area) |
| Authentication (Internal) | ASP.NET Core Identity (IdentityUser - already extended as ApplicationUser) |
| Authentication (Public) | ASP.NET Core Identity (separate PublicUser store) |
| Background Jobs | Hangfire or .NET BackgroundService |
| Document Generation | QuestPDF or similar for letter templating |
| Digital Signatures | SignaturePad.js (capture) + X.509 server-side verification |
| Notifications | Email (SMTP/SendGrid) + In-App notification queue |
| GIS Integration | NetTopologySuite + external GIS service APIs |

---

## 2. System Architecture

### 2.1 High-Level Architecture Diagram

```
+------------------------------------------------------------------+
|                        LOAD BALANCER / REVERSE PROXY              |
+------------------+-------------------+----------------------------+
                   |                   |
    +--------------v------+  +---------v--------------+
    |  INTERNAL MVC APP   |  |   PUBLIC PORTAL APP    |
    |  (DWS/CMA Officials)|  |   (Water Users)        |
    |  /Admin, /Property, |  |   /Portal/Login,       |
    |  /Validation, /Owner|  |   /Portal/Dashboard,   |
    |  /Workflow, /Reports|  |   /Portal/Cases,       |
    |  /Letters, /GIS     |  |   /Portal/Letters      |
    +---------+-----------+  +---------+--------------+
              |                        |
              +----------+-------------+
                         |
              +----------v-----------+
              |    SERVICE LAYER     |
              |  WorkflowEngine      |
              |  CalculatorEngine    |
              |  LetterService       |
              |  NotificationService |
              |  SignatureService     |
              |  ReportingService    |
              |  AuditService        |
              |  IntegrationService  |
              +----------+-----------+
                         |
              +----------v-----------+
              |   REPOSITORY LAYER   |
              |  (Data Access)       |
              +----------+-----------+
                         |
         +---------------+----------------+
         |               |                |
+--------v---+  +--------v-----+  +-------v--------+
| V&V SQL DB |  | WARMS (ext)  |  | eWULAAS (ext)  |
| (Primary)  |  | (Integration)|  | (Integration)  |
+------------+  +--------------+  +----------------+

         +---------------+----------------+
         |               |                |
+--------v--------+ +----v--------+ +----v-----------+
| Surveyor General| | Deeds Office| | Satellite/GIS  |
| (DRDLR)        | | (DRDLR)     | | Image Store    |
+-----------------+ +-------------+ +----------------+
```

### 2.2 Internal Application Layers

```
Controllers (HTTP boundary)
    |
    v
Services (Business logic, orchestration)
    |
    +---> WorkflowEngine (state machine, step transitions)
    +---> CalculatorEngine (SAPWAT, Dam volumes, SFRA)
    +---> LetterService (template rendering, PDF generation)
    +---> NotificationService (email, in-app, reminders)
    +---> SignatureService (capture, verify, store)
    +---> IntegrationService (WARMS, eWULAAS, SG, Deeds)
    +---> AuditService (immutable action log)
    |
    v
Repositories (Data access via EF Core)
    |
    v
ApplicationDBContext --> SQL Server
```

---

## 3. User Roles & Access Control

### 3.1 Internal Users (ApplicationUser - existing model, extends IdentityUser)

| Role | Description | Key Permissions |
|------|-------------|-----------------|
| **SystemAdmin** | IT administrator | Full system access, user management, configuration |
| **ProjectManager** | V&V project lead | All V&V functions, reporting, workflow override |
| **ValidationOfficer** | DWS/CMA official performing V&V | Create/edit files, run calculations, generate letters |
| **CaptureClerk** | Data entry personnel | Capture property, field & crop, dam data |
| **GISAnalyst** | Spatial analysis specialist | GIS analysis, field digitising, satellite image management |
| **RegionalManager** | DWS Regional Office / CMA head | View/approve within region, signing authority for Section 35 letters |
| **Auditor** | Read-only oversight | View all data, audit trail, reports |

### 3.2 Public Users (NEW: PublicUser - separate identity store)

| Role | Description | Key Permissions |
|------|-------------|-----------------|
| **WaterUser** | Property owner / water user | View own case, download letters, upload documents, sign letters electronically, update contact details |
| **Intermediary** | Agent/representative acting for water user(s) | Same as WaterUser but for linked clients |

### 3.3 Public User Data Model (NEW)

```csharp
// NEW MODEL - Separate from ApplicationUser
public class PublicUser
{
    public Guid PublicUserId { get; set; }

    // Identity fields
    public string EmailAddress { get; set; }        // Login credential
    public string PasswordHash { get; set; }        // Managed by Identity
    public string PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }

    // Personal details
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string IdentityNumber { get; set; }      // SA ID or Passport
    public string BusinessRegistrationNumber { get; set; } // Optional, for entities

    // Link to existing domain
    public Guid? PropertyOwnerId { get; set; }      // FK to PropertyOwner once verified
    public PropertyOwner? PropertyOwner { get; set; }

    // Account management
    public DateTime RegistrationDate { get; set; }
    public PublicUserStatus Status { get; set; }     // Pending, Active, Suspended
    public PublicUserRole Role { get; set; }         // WaterUser, Intermediary
}

public enum PublicUserStatus { Pending, Active, Suspended, Deactivated }
public enum PublicUserRole { WaterUser, Intermediary }
```

### 3.4 Authentication Architecture

```
INTERNAL USERS                         PUBLIC USERS
+-----------------+                    +--------------------+
| ASP.NET Identity|                    | ASP.NET Identity   |
| (ApplicationUser|                    | (PublicUser store)  |
| + Role claims)  |                    | + Role claims)     |
+--------+--------+                    +---------+----------+
         |                                       |
    Cookie Auth                             Cookie Auth
    /Admin area                             /Portal area
         |                                       |
+--------v--------+                    +---------v----------+
| Internal MVC    |                    | Public Portal      |
| Controllers     |                    | Controllers        |
+-----------------+                    +--------------------+
         |                                       |
         +------ Shared Service Layer -----------+
         |                                       |
         +------ Shared Database ----------------+
```

**Key Design Decision:** Use two separate ASP.NET Core Identity user stores within the same database but different tables (`ApplicationUsers` for internal, `PublicUsers` for portal). This provides:
- Complete isolation of credentials and roles
- Different password policies (internal may require MFA, public uses email verification)
- No risk of a public user accessing internal functions
- Shared data layer for reading property/case data

---

## 4. Core Domain Model Relationships

### 4.1 Entity Relationship Summary

```
PropertyOwner ----< PropertyOwnership >---- Property
     |                                         |
     |                                    +----+--------+------+------+
     v                                    |    |        |      |      |
  Address                           Entitlement |   FieldAndCrop |  Storing
     ^                                    |    |        |      |      |
     |                              Forestation |  Irrigation    |  DamCalc
     +--- Property.PropertyAddress        |    |        |      |      |
     +--- GovernmentWaterControlArea      v    v        v      v      v
     +--- IrrigationBoard           EntitlementType  Crop  WaterSource River
                                              |       |
                                              v       v
                                        (lookup)  CropType

FileMaster ----------> Property (core registration file)
     |
     +---> ApplicationUser (validated_by)
     +---> ApplicationUser (captured_by)
     +---> ValidationStatus

Validation ----------> Property, Period, Entitlement
LetterIssuance ------> PropertyOwner, LetterType
```

### 4.2 Missing Relationships to Add

The following relationships are implied by the V&V process but not yet in the data model:

| Entity | Missing Relationship | Purpose |
|--------|---------------------|---------|
| `FileMaster` | `ICollection<LetterIssuance>` | Track all letters issued per file |
| `FileMaster` | `ICollection<Document>` | Attach uploaded documents (new entity) |
| `Property` | `ICollection<SateliteImage>` | Link satellite images to properties |
| `Validation` | `FileMaster` FK | Link validation case to its file |
| `Validation` | `ApplicationUser AssignedTo` | Who is working on this case |
| NEW: `WorkflowInstance` | Full workflow state tracking | See Section 5 |
| NEW: `WorkflowStep` | Individual step completion | See Section 5 |
| NEW: `Document` | File/document storage | For uploaded evidence |
| NEW: `AuditLog` | Immutable action log | See Section 14 |
| NEW: `Notification` | Notification queue | See Section 10 |
| NEW: `DigitalSignature` | Signature records | See Section 11 |

---

## 5. V&V Workflow Engine - Phase 1: Project Inception

### 5.1 Workflow Engine Data Model

```csharp
public class WorkflowInstance
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid FileMasterId { get; set; }           // One workflow per FileMaster
    public FileMaster FileMaster { get; set; }
    public WorkflowPhase CurrentPhase { get; set; }  // Inception, Verification, Validation
    public WorkflowStepEnum CurrentStep { get; set; }
    public WorkflowStatus Status { get; set; }       // Active, Paused, Completed, Cancelled
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid AssignedToId { get; set; }
    public ApplicationUser AssignedTo { get; set; }
    public ICollection<WorkflowStepRecord> Steps { get; set; }
}

public class WorkflowStepRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; }
    public WorkflowStepEnum Step { get; set; }
    public StepStatus Status { get; set; }           // Pending, InProgress, Completed, Skipped
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? CompletedById { get; set; }
    public ApplicationUser? CompletedBy { get; set; }
    public string? Notes { get; set; }
    public string? ValidationErrors { get; set; }    // JSON of any blocking issues
}

public enum WorkflowPhase
{
    PROJECT_INCEPTION,       // Phase 1
    STUDY_IMPLEMENTATION,    // Phase 2: Verification
    SECTION_35_VALIDATION    // Phase 3: Validation
}

public enum WorkflowStepEnum
{
    // Phase 1: Project Inception (Control Points 1-5)
    CP01_OBTAIN_WARMS_DATABASE_AUDIT,
    CP02_IDENTIFY_UNREGISTERED_USERS,
    CP03_DATABASE_ANALYSIS_RECORD_STATUS,
    CP04_PROJECT_INCEPTION_REPORT,
    CP05_PUBLIC_PARTICIPATION_SESSION_1,

    // Phase 2: Verification (Control Points 6-16)
    CP06_EVALUATE_WARMS_REGISTRATION,
    CP07_OBTAIN_SPATIAL_INFORMATION,
    CP08_OBTAIN_SUPPORTIVE_INFORMATION,
    CP09_PROPERTY_SOURCE_EVALUATION,
    CP10_EVALUATE_ADDITIONAL_INFORMATION,
    CP11_GIS_ANALYSIS_FIELD_DIGITISING,
    CP12_FIELD_AND_CROP_MODELLING,
    CP13_CALCULATE_ELU_DISCREPANCY,
    CP14_UPDATE_VV_DATABASE,
    CP15_CALCULATE_DAM_VOLUMES,
    CP16_CALCULATE_STREAM_FLOW_REDUCTION,

    // Phase 3: Validation / Section 35 (Control Points 17-21)
    CP17_PUBLIC_PARTICIPATION_SESSION_2,
    CP18_FIELD_VISIT_LETTER_DISSEMINATION,
    CP19_SECTION_35_CORRESPONDENCE,
    CP20_SECTION_35_PROCESS_TRACKING,
    CP21_PROJECT_CONTROL_REPORTING
}

public enum StepStatus { Pending, InProgress, Completed, Skipped, Blocked }
public enum WorkflowStatus { Active, Paused, Completed, Cancelled }
```

### 5.2 Phase 1 Workflow: Project Inception

```
+===========================================================+
|  PHASE 1: PROJECT INCEPTION                                |
+===========================================================+
|                                                            |
|  CP01: Obtain WARMS Database & Perform Audit               |
|  +----------------------------------------------------+   |
|  | INPUT:  Latest WARMS database extract               |   |
|  | PROCESS:                                            |   |
|  |   - Compare WARMS records against V&V database      |   |
|  |   - Identify new records in WARMS not in V&V        |   |
|  |   - Detect ownership changes                        |   |
|  |   - Detect title deed changes                       |   |
|  | OUTPUT: Audit report, new FileMaster records created |   |
|  | GATE:   Audit report reviewed and approved           |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP02: Identify Unregistered Users                         |
|  +----------------------------------------------------+   |
|  | INPUT:  Current V&V data + latest satellite imagery |   |
|  | PROCESS:                                            |   |
|  |   - GIS analysis overlaying property boundaries     |   |
|  |     on satellite imagery                            |   |
|  |   - Compare with WARMS registrations                |   |
|  |   - Flag properties with water use but no           |   |
|  |     WARMS registration                              |   |
|  | OUTPUT: List of unregistered users, new FileMaster   |   |
|  |         records for unregistered users               |   |
|  | GATE:   Unregistered user list reviewed              |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP03: V&V Database Analysis - Record Status Quo           |
|  +----------------------------------------------------+   |
|  | INPUT:  All FileMaster records                      |   |
|  | PROCESS:                                            |   |
|  |   - Categorise all records:                         |   |
|  |     * Completed (validated)                         |   |
|  |     * Verification complete, awaiting validation    |   |
|  |     * In Section 35 process (incomplete)            |   |
|  |     * Not yet started                               |   |
|  | OUTPUT: Status report per category & per catchment  |   |
|  | GATE:   Status report approved by Project Manager   |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP04: Project Inception Report                            |
|  +----------------------------------------------------+   |
|  | INPUT:  Outputs from CP01-CP03                      |   |
|  | PROCESS:                                            |   |
|  |   - Compile inception report covering:              |   |
|  |     * Scope of catchment                            |   |
|  |     * Total records, new records, status breakdown  |   |
|  |     * Resource plan and timeline                    |   |
|  | OUTPUT: Inception report document                   |   |
|  | GATE:   Report signed off by Regional Manager       |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP05: Public Participation Session 1                      |
|  +----------------------------------------------------+   |
|  | INPUT:  Inception report, stakeholder lists         |   |
|  | PROCESS:                                            |   |
|  |   - Large-scale public launch per WMA               |   |
|  |   - Media, industry, organised agriculture unions   |   |
|  |   - Inform stakeholders of process and timelines    |   |
|  | OUTPUT: Attendance registers, session minutes        |   |
|  | GATE:   Session completed, minutes approved          |   |
|  +----------------------------------------------------+   |
|                                                            |
|  >>> TRANSITION TO PHASE 2 (per property) >>>              |
+============================================================+
```

### 5.3 Data Flow: Phase 1

```
WARMS DB -----> [CP01: Audit] -----> New FileMaster records
                     |                      |
                     v                      v
              Audit Report          V&V Database (FileMaster table)
                                           |
Satellite    -----> [CP02: GIS] -----> Unregistered Users List
Imagery              |                      |
                     v                      v
              GIS Analysis           New FileMaster records
              Results                (PropertyIndex = "Unregistered")
                                           |
V&V Database -----> [CP03: Status] --> Status Report
                     |                      |
                     v                      v
              Category counts        Dashboard metrics
                                           |
CP01+02+03 -----> [CP04: Report] --> Inception Report (Document)
                                           |
                                           v
Stakeholders <---- [CP05: Public] <-- Presentation materials
                     |
                     v
              Attendance + Minutes (Documents)
```

---

## 6. V&V Workflow Engine - Phase 2: Verification Process

### 6.1 Phase 2 Workflow: Study Implementation (per property)

This phase runs **per FileMaster record** (per property). Each property moves through control points CP06-CP16 independently.

```
+===========================================================+
|  PHASE 2: VERIFICATION (per FileMaster / Property)         |
+===========================================================+
|                                                            |
|  CP06: Evaluate WARMS Registration Information             |
|  +----------------------------------------------------+   |
|  | INPUT:  WARMS report for this property              |   |
|  | PROCESS:                                            |   |
|  |   - Identify registered activities (Taking,         |   |
|  |     Storing, Industrial, SFRA, etc.)                |   |
|  |   - Study registration info: volumes, storage,      |   |
|  |     sources, crop fields, hectares, crop types,     |   |
|  |     rotation factor, irrigation methods             |   |
|  | OUTPUT: Populated FileMaster fields                  |   |
|  |   (RegisteredForTakingWater, RegisteredForStoring,  |   |
|  |    RegisteredForForestation, etc.)                   |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - FileMaster (registration flags)                 |   |
|  |   - Irrigation (initial WARMS data)                 |   |
|  |   - Storing (initial WARMS data)                    |   |
|  |   - Forestation (initial WARMS data)                |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP07: Obtain Supportive Spatial Information               |
|  +----------------------------------------------------+   |
|  | INPUT:  Property SG code, coordinates               |   |
|  | PROCESS:                                            |   |
|  |   - Obtain SG farm boundaries & subdivisions        |   |
|  |   - Catchment & management boundaries               |   |
|  |   - Rivers and dams layer                           |   |
|  |   - Government Control Area boundaries              |   |
|  |   - Satellite imagery (1996-1999 qualify period)    |   |
|  |   - Latest current satellite images                 |   |
|  |   - Ortho photos and topo cadastral images          |   |
|  | OUTPUT: GIS layers linked to property               |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - SateliteImage (imagery records)                 |   |
|  |   - Property (SGCode, coordinates verification)     |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP08: Obtain Other Supportive Information                 |
|  +----------------------------------------------------+   |
|  | INPUT:  Historical records for the property         |   |
|  | PROCESS:                                            |   |
|  |   - Previous field surveys & data                   |   |
|  |   - Government Water Control Area & date            |   |
|  |   - Irrigation Board membership                     |   |
|  |   - Section 9B applicability                        |   |
|  |   - Government Water Scheme                         |   |
|  |   - Riparian farm status                            |   |
|  | OUTPUT: Context information for ELU determination   |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - FileMaster (GWCA, IrrigationBoard, etc.)        |   |
|  |   - Validation (supporting detail fields)           |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP09: Property & Source Evaluation                        |
|  +----------------------------------------------------+   |
|  | INPUT:  Title deeds, SG diagram, WARMS data, GIS   |   |
|  | PROCESS:                                            |   |
|  |   - Obtain title deeds and SG diagram               |   |
|  |   - Evaluate title deed owner vs WARMS              |   |
|  |   - Evaluate 1st registration date, subdivisions,   |   |
|  |     consolidations                                  |   |
|  |   - Compare SG diagram size with GIS property       |   |
|  |   - Evaluate water source per WARMS                 |   |
|  |   - Evaluate property location vs impacting factors |   |
|  |     (GWCA, Irrigation Board, Section 9B, GWS,      |   |
|  |      riparian farm status)                          |   |
|  | OUTPUT: Verified property & source information      |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - Property (verified size, dates, SG code)        |   |
|  |   - PropertyOwner (verified from title deed)        |   |
|  |   - PropertyOwnership (title deed number, date)     |   |
|  |   - WaterSource (verified sources)                  |   |
|  | GATE: Property evaluation form completed            |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP10: Evaluate Additional Information                     |
|  +----------------------------------------------------+   |
|  | INPUT:  Historical permits, authorisations, etc.    |   |
|  | PROCESS:                                            |   |
|  |   - Evaluate property relevance to:                 |   |
|  |     * Permits (old Water Act)                       |   |
|  |     * Section 32/33 approvals                       |   |
|  |     * Transfers (temporary/permanent)               |   |
|  |     * Ad-hoc field surveys                          |   |
|  |     * Water Court decisions                         |   |
|  |     * Licences, General Authorisations              |   |
|  |     * Other authorisations                          |   |
|  | OUTPUT: Additional info captured per property       |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - NEW: AdditionalInformation entity               |   |
|  |   - Entitlement (type determined)                   |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP11: GIS Analysis & Field Digitising                     |
|  +----------------------------------------------------+   |
|  | INPUT:  GIS layers, satellite imagery, property     |   |
|  | PROCESS:                                            |   |
|  |   - Identify property on GIS                        |   |
|  |   - Overlay boundaries on qualify period imagery    |   |
|  |   - Overlay boundaries on current period imagery    |   |
|  |   - Evaluate irrigation fields & storage structures |   |
|  |   - Evaluate winter/summer images for crop rotation |   |
|  |   - Compare GIS analysis with WARMS registration    |   |
|  |   - Digitise crop fields & storage structures       |   |
|  |     for qualify and current periods                 |   |
|  |   - Calculate field hectares                        |   |
|  |   - Calculate dam storage                           |   |
|  |   - Create GIS maps for qualify & current periods   |   |
|  | OUTPUT: Digitised field boundaries, calculated areas|   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - FieldAndCrop (field area, field number)         |   |
|  |   - SateliteImage (reference images used)           |   |
|  |   - DamCalculation (initial dam identification)     |   |
|  |   - NEW: Document (GIS maps as PDF/image)           |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP12: Field & Crop + SAPWAT Modelling                     |
|  +----------------------------------------------------+   |
|  | INPUT:  Digitised fields from CP11                  |   |
|  | PROCESS:                                            |   |
|  |   For each field (qualify + current period):        |   |
|  |   - Satellite image ref & date                      |   |
|  |   - Field area (ha)                                 |   |
|  |   - Crop name & plant date                          |   |
|  |   - Rotation factor (%)                             |   |
|  |   - Irrigation system code                          |   |
|  |   - Water source (type, name, %)                    |   |
|  |   - Crop area (ha)                                  |   |
|  |   - SAPWAT calculation (mm/ha/a)                    |   |
|  |   >>> CALLS CALCULATOR ENGINE (SAPWAT) <<<          |   |
|  | OUTPUT: Complete field/crop data with volumes       |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - FieldAndCrop (all fields populated)             |   |
|  |   - FieldAndCrop.SAPWATCalculationResult            |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP13: Calculate ELU / Lawful / Unlawful                   |
|  +----------------------------------------------------+   |
|  | INPUT:  All verification data from CP06-CP12        |   |
|  | PROCESS:                                            |   |
|  |   >>> CALLS CALCULATOR ENGINE (ELU) <<<             |   |
|  |   For each scenario (by water source S/B):          |   |
|  |   - Proclamation Date use                           |   |
|  |   - Previous Surveys use                            |   |
|  |   - Possible Existing use                           |   |
|  |   - Possible Lawful use (GWCA/Permit/IB/GWS)       |   |
|  |   - Possible Existing Lawful use                    |   |
|  |   - Possible Unlawful use                           |   |
|  |   - Registered use                                  |   |
|  |   - Present day use                                 |   |
|  |   - Possible Unlawful Increase/Decrease             |   |
|  |   - General Authorisation                           |   |
|  |   Each with: Land ha, Crop ha, Volume m3, Source    |   |
|  | OUTPUT: Complete ELU determination per scenario     |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - Irrigation (all scenario fields populated)      |   |
|  |   - Entitlement (volume determined)                 |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|          v                                                 |
|  CP14: Update V&V Database                                 |
|  +----------------------------------------------------+   |
|  | INPUT:  All calculated data from CP13               |   |
|  | PROCESS:                                            |   |
|  |   - Capture all calculated information per property |   |
|  |   - Link to WARMS registration number               |   |
|  |   - Update FileMaster validation status             |   |
|  | OUTPUT: Complete V&V record for property            |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - FileMaster.GetValidationStatus updated          |   |
|  |   - All linked tables verified & populated          |   |
|  | GATE: Data reviewed and approved by senior officer  |   |
|  +----------------------------------------------------+   |
|          |                                                 |
|     +----+----+                                            |
|     |         |                                            |
|     v         v                                            |
|  CP15       CP16   (can run in parallel)                   |
|                                                            |
|  CP15: Calculate Dam Volumes                               |
|  +----------------------------------------------------+   |
|  | INPUT:  GIS dam identification, property data       |   |
|  | PROCESS:                                            |   |
|  |   >>> CALLS CALCULATOR ENGINE (Dam Volumes) <<<     |   |
|  |   For each dam:                                     |   |
|  |   - Wall length, fetch, depth (from GIS)            |   |
|  |   - Factor (triangular=0.33, square=0.4, circ=0.5) |   |
|  |   - Capacity = WallLength x Fetch x Depth x Factor |   |
|  |   OR: Area(ha) x Depth(m) x Factor(3,4,5) x 1000  |   |
|  | OUTPUT: Dam capacities per property                 |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - DamCalculation (capacity, river, status)        |   |
|  |   - Storing (volumes per scenario)                  |   |
|  +----------------------------------------------------+   |
|                                                            |
|  CP16: Calculate Stream Flow Reduction (Forestation)       |
|  +----------------------------------------------------+   |
|  | INPUT:  Forestation data, GWCA status               |   |
|  | PROCESS:                                            |   |
|  |   >>> CALLS CALCULATOR ENGINE (SFRA) <<<            |   |
|  |   - Qualify period SFRA hectares & volume           |   |
|  |   - Current period SFRA hectares & volume           |   |
|  |   - Registered hectares/volume vs actual            |   |
|  |   - ELU hectares/volume                             |   |
|  |   - Unlawful hectares/volume                        |   |
|  |   - Lawful hectares/volume                          |   |
|  |   - Pre-1972 hectares/volume                        |   |
|  | OUTPUT: SFRA volumes per property                   |   |
|  | DATA WRITTEN TO:                                    |   |
|  |   - Forestation (all fields populated)              |   |
|  +----------------------------------------------------+   |
|                                                            |
|  >>> VERIFICATION COMPLETE - TRANSITION TO PHASE 3 >>>     |
+============================================================+
```

### 6.2 Data Flow: Phase 2 (per property)

```
                    WARMS
                      |
                      v
              [CP06: Evaluate WARMS]
                      |
         +------------+------------+
         |            |            |
         v            v            v
    Irrigation    Storing    Forestation
    (initial)    (initial)   (initial)
         |            |            |
         v            v            v
    [CP07: Spatial]  [CP08: Supportive Info]
         |            |
         v            v
  SateliteImage   FileMaster (GWCA, IB, etc.)
         |            |
         +------+-----+
                |
                v
        [CP09: Property & Source Eval]
                |
    +-----------+-----------+
    |           |           |
    v           v           v
  Property  PropertyOwner  WaterSource
  (verified) (from deeds)  (verified)
                |
                v
        [CP10: Additional Info]
                |
                v
        AdditionalInformation
        Entitlement (type)
                |
                v
        [CP11: GIS Analysis]
                |
        +-------+-------+
        |               |
        v               v
  FieldAndCrop     DamCalculation
  (field area)     (identified)
        |
        v
  [CP12: SAPWAT Modelling]  <<<--- CALCULATOR ENGINE
        |
        v
  FieldAndCrop.SAPWATCalculationResult
        |
        v
  [CP13: Calculate ELU]     <<<--- CALCULATOR ENGINE
        |
        v
  Irrigation (all scenarios populated)
  Entitlement (volume)
        |
        v
  [CP14: Update Database]
        |
  +-----+-----+
  |           |
  v           v
[CP15]      [CP16]           <<<--- CALCULATOR ENGINE (both)
  |           |
  v           v
DamCalc    Forestation
Storing    (all fields)
  |           |
  +-----+-----+
        |
        v
  VERIFICATION COMPLETE
  FileMaster.ValidationStatus = "Completed"
```

---

## 7. V&V Workflow Engine - Phase 3: Validation (Section 35) Process

### 7.1 Section 35 Letter Flow

The Section 35 process is the **legal validation** step where water users are formally notified of their determined ELU and given the opportunity to respond.

```
+-----------------------------------------------------------------+
|  PHASE 3: VALIDATION / SECTION 35 PROCESS                       |
+-----------------------------------------------------------------+
|                                                                  |
|  CP17: Public Participation Session 2                            |
|  +-----------------------------------------------------------+  |
|  | - Detail field visit sessions per small area               |  |
|  | - Geographic distribution by quaternary catchment          |  |
|  | - Invite individual farmers, unions, industries            |  |
|  | - Opening session: regulations & requirements              |  |
|  | - Individual sessions: discuss files & findings            |  |
|  | - Present application letters (Letter 1)                   |  |
|  | - Arrange follow-up meetings                               |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|          v                                                       |
|  CP18-20: Section 35 Letter Tracking                             |
|                                                                  |
|  The following letter sequence is tracked per property:          |
|                                                                  |
|  LETTER 1: Section 35(1) - Notice to apply for verification     |
|  +-----------------------------------------------------------+  |
|  | Generated ---> Signed (RegionalManager) ---> Issued        |  |
|  | Issue Method: Registered post / email / in person          |  |
|  | Due Date set (typically 60 days)                           |  |
|  |                                                            |  |
|  | RESPONSE TRACKING:                                         |  |
|  |   +-- Application Returned (Agreed) --> LETTER 3           |  |
|  |   +-- Application Returned (Not Agreed) --> LETTER 2       |  |
|  |   +-- Failure to Respond --> REISSUE LETTER 1              |  |
|  |   +-- Return to Sender (RTS) --> Investigate, REISSUE      |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|     [If reissued]                                                |
|          v                                                       |
|  LETTER 1 REISSUE                                                |
|  +-----------------------------------------------------------+  |
|  | Same tracking as Letter 1                                  |  |
|  | If still no response --> LETTER 1A                          |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|          v                                                       |
|  LETTER 1A: Section 53(1) - Directive to apply for verification  |
|  +-----------------------------------------------------------+  |
|  | Escalation: legal directive to comply                      |  |
|  | Due Date set                                               |  |
|  | Response tracking same as Letter 1                         |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|     [If not agreed / additional info needed]                     |
|          v                                                       |
|  LETTER 2: Section 35(3)(a) - Request additional information     |
|  +-----------------------------------------------------------+  |
|  | Specific information request based on disputes              |  |
|  | Due Date set                                               |  |
|  | RESPONSE:                                                  |  |
|  |   +-- Info Returned --> Re-evaluate --> LETTER 3            |  |
|  |   +-- Failure to Respond --> LETTER 2A                      |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|          v                                                       |
|  LETTER 2A: Section 53(1) - Directive to provide information     |
|  +-----------------------------------------------------------+  |
|  | Legal directive to provide additional information           |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|          v                                                       |
|  LETTER 3: Section 35(4) - Confirmation of ELU extent & lawful   |
|  +-----------------------------------------------------------+  |
|  | FINAL DETERMINATION                                        |  |
|  | Confirms:                                                  |  |
|  |   - Extent of existing lawful use (volumes, hectares)      |  |
|  |   - Lawfulness determination                               |  |
|  |   - Any unlawful use identified                            |  |
|  | Requires: Digital signature of Regional Manager             |  |
|  | Legal status: Same as a water use licence                  |  |
|  | >>> TRIGGERS: NOTIFICATION TO PUBLIC PORTAL <<<             |  |
|  | >>> TRIGGERS: WARMS UPDATE via Integration <<<              |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|     [If unlawful use identified]                                 |
|          v                                                       |
|  LETTER 4A: Section 53(1) - Notice of intent to direct stop     |
|  +-----------------------------------------------------------+  |
|  | Warning notice before enforcement                          |  |
|  +-----------------------------------------------------------+  |
|          |                                                       |
|          v                                                       |
|  LETTER 4&5: Section 53(1) - Directive to stop unlawful use     |
|  +-----------------------------------------------------------+  |
|  | Enforcement action                                         |  |
|  +-----------------------------------------------------------+  |
|                                                                  |
+-----------------------------------------------------------------+
```

### 7.2 Letter Tracking Data Model

```csharp
// Enhanced LetterIssuance (replaces current minimal model)
public class LetterIssuance
{
    public Guid Id { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster FileMaster { get; set; }
    public Guid PropertyOwnerId { get; set; }
    public PropertyOwner PropertyOwner { get; set; }
    public Guid LetterTypeId { get; set; }
    public LetterType LetterType { get; set; }

    // Generation & Signing
    public DateOnly GeneratedDate { get; set; }
    public DateOnly? SignedDate { get; set; }
    public Guid? SignedById { get; set; }
    public ApplicationUser? SignedBy { get; set; }
    public Guid? DigitalSignatureId { get; set; }

    // Issuance
    public DateOnly? IssuedDate { get; set; }
    public LetterIssueMethod? IssueMethod { get; set; }
    public DateOnly? DueDate { get; set; }

    // Response Tracking
    public LetterResponseStatus ResponseStatus { get; set; }
    public DateOnly? ResponseDate { get; set; }
    public bool? AgreedWithFindings { get; set; }
    public string? ResponseNotes { get; set; }
    public bool ReturnedToSender { get; set; }

    // Batch management
    public string? BatchNumber { get; set; }

    // Link to generated document
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    // Reissue tracking
    public Guid? ReissuedFromId { get; set; }
    public LetterIssuance? ReissuedFrom { get; set; }
    public Guid? EscalatedToId { get; set; }      // Next letter in chain
    public LetterIssuance? EscalatedTo { get; set; }
}

public enum LetterIssueMethod { RegisteredPost, Email, InPerson, Other }
public enum LetterResponseStatus
{
    Pending, AwaitingResponse, ReturnedAgreed, ReturnedNotAgreed,
    FailureToRespond, ReturnToSender, Reissued, ConvertedToDirective
}
```

### 7.3 Section 35 State Machine

```
                    [LETTER 1 Generated]
                           |
                           v
                    [LETTER 1 Signed]
                           |
                           v
                    [LETTER 1 Issued]
                           |
                    (wait for response)
                           |
            +--------------+--------------+---------------+
            |              |              |               |
            v              v              v               v
      [Agreed]      [Not Agreed]   [No Response]    [RTS]
            |              |              |               |
            v              v              v               v
       LETTER 3      LETTER 2      REISSUE L1      Investigate
            |              |              |           then REISSUE
            |              |              |               |
            |         (wait resp)   (wait response)       |
            |              |         +---------+          |
            |              |         |         |          |
            |              v         v         v          |
            |        [Info Recv]  [Agreed] [No Resp]      |
            |              |         |         |          |
            |              v         v         v          |
            |         Re-evaluate  L3    LETTER 1A------->+
            |              |                   |
            |              v                   v
            |           LETTER 3          (directive)
            |              |                   |
            v              v                   v
      [LETTER 3: ELU Confirmed]          LETTER 2A
            |                                  |
            |                                  v
            |                            (forced info)
            |                                  |
            |                                  v
            |                             LETTER 3
            |                                  |
            +----------------------------------+
            |
            v
      [V&V COMPLETE for this property]
            |
      +-----+-----+
      |           |
      v           v
  [Lawful]   [Unlawful portion]
      |           |
      v           v
  Update      LETTER 4A --> LETTER 4&5
  WARMS       (enforcement)
```

---

## 8. Calculator Engine Design

### 8.1 Architecture

The Calculator Engine is a stateless service layer that performs all mathematical computations.

```csharp
public interface ICalculatorEngine
{
    // SAPWAT Water Requirement Calculation
    SapwatResult CalculateSapwat(SapwatInput input);

    // ELU Determination across all scenarios
    EluResult CalculateElu(EluInput input);

    // Dam Volume Calculation
    DamVolumeResult CalculateDamVolume(DamVolumeInput input);

    // Stream Flow Reduction Activity (Forestation)
    SfraResult CalculateSfra(SfraInput input);

    // Irrigation volume from field & crop data
    IrrigationVolumeResult CalculateIrrigationVolume(IrrigationVolumeInput input);
}
```

### 8.2 SAPWAT Calculation

```csharp
public class SapwatInput
{
    public string CropName { get; set; }
    public string ClimateZone { get; set; }
    public string IrrigationSystemCode { get; set; }
    public decimal CropAreaHa { get; set; }
    public decimal RotationFactor { get; set; }     // percentage
    public DateOnly PlantDate { get; set; }
}

public class SapwatResult
{
    public decimal MmPerHaPerAnnum { get; set; }     // mm/ha/a
    public decimal TotalVolumeM3 { get; set; }       // m3
    public decimal[] MonthlyRequirements { get; set; } // 12 months in m3
}
```

### 8.3 Dam Volume Calculation

Per Appendix D of the V&V Guide:

```csharp
public class DamVolumeInput
{
    // Method 1: Wall-based
    public decimal? WallLengthM { get; set; }
    public decimal? FetchM { get; set; }
    public decimal? DepthM { get; set; }        // Calculated: Fetch / Slope
    public DamShapeFactor ShapeFactor { get; set; }

    // Method 2: Area-based
    public decimal? AreaHa { get; set; }

    // For depth calculation
    public decimal? RiverDistanceBetweenContours { get; set; }
    public decimal? ContourDifference { get; set; }
}

public enum DamShapeFactor
{
    Triangular,      // Factor = 0.33
    SquareWithBend,  // Factor = 0.4
    Circular         // Factor = 0.5
}

public class DamVolumeResult
{
    public decimal CapacityM3 { get; set; }
    public decimal DepthM { get; set; }
    public string CalculationMethod { get; set; }  // "WallBased" or "AreaBased"
}
```

### 8.4 ELU Determination

```csharp
public class EluInput
{
    public Guid PropertyId { get; set; }

    // All scenario data for irrigation (surface + borehole)
    public ScenarioData ProclamationDate { get; set; }
    public ScenarioData PreviousSurveys { get; set; }
    public ScenarioData PossibleExisting { get; set; }
    public ScenarioData PossibleLawful { get; set; }
    public ScenarioData Registration { get; set; }
    public ScenarioData PresentDayUse { get; set; }
    public ScenarioData GeneralAuthorisation { get; set; }

    // Context
    public bool IsWithinGWCA { get; set; }
    public bool HasPermit { get; set; }
    public bool IsRiparianFarm { get; set; }
    public bool HasIrrigationBoard { get; set; }
    public bool HasGovernmentWaterScheme { get; set; }
}

public class ScenarioData
{
    public decimal SurfaceLandHa { get; set; }
    public decimal SurfaceCropHa { get; set; }
    public decimal SurfaceVolumeM3 { get; set; }
    public string SurfaceSource { get; set; }
    public decimal BoreholeLandHa { get; set; }
    public decimal BoreholeCropHa { get; set; }
    public decimal BoreholeVolumeM3 { get; set; }
    public string BoreholeSource { get; set; }
    public DateOnly? Date { get; set; }
}

public class EluResult
{
    public ScenarioData PossibleExistingLawful { get; set; }
    public ScenarioData PossibleUnlawful { get; set; }
    public ScenarioData PossibleUnlawfulIncreaseDecrease { get; set; }
    public decimal TotalEluVolumeM3 { get; set; }
    public decimal TotalUnlawfulVolumeM3 { get; set; }
    public string Determination { get; set; }  // Summary
}
```

### 8.5 Calculator Engine Data Flow

```
FieldAndCrop data ----+
Crop + CropType ------+----> [SAPWAT Calculator] ----> SAPWATCalculationResult
IrrigationSystem -----+                                        |
ClimateZone ----------+                                        |
                                                               v
WARMS Data -----+                                    [ELU Calculator]
GIS Analysis ---+                                         |
GWCA/IB/GWS ----+--> [ELU Scenario Builder] ------------>|
Permit Data ----+                                         |
                                                          v
                                                   ELU Determination
                                                   (Irrigation table scenarios)
                                                          |
                                                          v
GIS Dam Data ---+                                  [Dam Volume Calc]
Contour Data ---+--> [Dam Calculator] ------------> DamCalculation.Capacity
                                                          |
                                                          v
                                                   Storing (volumes)

Forestation Data --+
GWCA Status -------+--> [SFRA Calculator] --------> Forestation (all volumes)
```

---

## 9. Letter Generation & Correspondence Engine

### 9.1 Architecture

```csharp
public interface ILetterService
{
    // Generate a letter from template with property/owner data
    Task<Document> GenerateLetter(Guid fileMasterId, LetterTypeEnum letterType);

    // Batch generation for multiple properties
    Task<ICollection<Document>> GenerateLetterBatch(
        ICollection<Guid> fileMasterIds, LetterTypeEnum letterType, string batchNumber);

    // Submit for signing
    Task<LetterIssuance> SubmitForSigning(Guid letterIssuanceId, Guid signerId);

    // Issue letter (post, email, in-person)
    Task<LetterIssuance> IssueLetter(
        Guid letterIssuanceId, LetterIssueMethod method, DateOnly dueDate);

    // Record response
    Task<LetterIssuance> RecordResponse(
        Guid letterIssuanceId, LetterResponseStatus status,
        bool? agreed, string? notes);

    // Get overdue letters
    Task<ICollection<LetterIssuance>> GetOverdueLetters(int daysOverdue);
}
```

### 9.2 Letter Templates

| Letter | NWA Section | Template Variables |
|--------|------------|-------------------|
| Letter 1 | 35(1) | Owner name, Property ref, Registration no, ELU summary, Due date |
| Letter 1A | 53(1) | Same as L1 + directive language |
| Letter 2 | 35(3)(a) | Owner name, Property ref, Specific info requested, Due date |
| Letter 2A | 53(1) | Same as L2 + directive language |
| Letter 3 | 35(4) | Owner name, Property ref, **Full ELU determination**, Volumes, Hectares, Sources, Lawfulness status |
| Letter 4A | 53(1) | Owner name, Property ref, Unlawful use details, Intent notice |
| Letter 4&5 | 53(1) | Owner name, Property ref, Directive to stop, Enforcement details |

### 9.3 Data Flow: Letter Generation

```
FileMaster --------+
PropertyOwner -----+
Property ----------+----> [LetterService.GenerateLetter()]
Irrigation ---------+            |
Storing ------------+            v
Forestation --------+     [Template Engine (QuestPDF)]
Entitlement --------+            |
                                 v
                          Document (PDF blob)
                                 |
                                 v
                          LetterIssuance record
                                 |
                    +------------+------------+
                    |                         |
                    v                         v
            [Submit for Signing]      [Store in Documents]
                    |
                    v
            [SignatureService]
                    |
                    v
            [Issue Letter]
                    |
            +-------+-------+--------+
            |       |       |        |
            v       v       v        v
          Post    Email   Person   Other
            |       |       |        |
            v       v       v        v
     [Track Response - Notification reminders start]
```

---

## 10. Notification Engine

### 10.1 Architecture

```csharp
public interface INotificationService
{
    // Send notification to internal user
    Task SendToUser(Guid userId, NotificationType type, string subject, string body);

    // Send notification to public portal user
    Task SendToPublicUser(Guid publicUserId, NotificationType type, string subject, string body);

    // Send overdue reminders (called by background job)
    Task ProcessOverdueReminders();

    // Send workflow step reminders
    Task ProcessWorkflowReminders();

    // Get unread notifications for a user
    Task<ICollection<Notification>> GetUnread(Guid userId, UserType userType);

    // Mark as read
    Task MarkAsRead(Guid notificationId);
}

public class Notification
{
    public Guid NotificationId { get; set; }

    // Polymorphic recipient
    public Guid? ApplicationUserId { get; set; }
    public Guid? PublicUserId { get; set; }

    public NotificationType Type { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public string? ActionUrl { get; set; }       // Deep link to relevant page

    public DateTime CreatedDate { get; set; }
    public DateTime? ReadDate { get; set; }
    public bool IsRead { get; set; }

    // For email notifications
    public bool EmailSent { get; set; }
    public DateTime? EmailSentDate { get; set; }
}

public enum NotificationType
{
    // Workflow
    WorkflowStepAssigned,
    WorkflowStepOverdue,
    WorkflowStepCompleted,

    // Letters
    LetterReadyForSigning,
    LetterIssued,
    LetterResponseOverdue,
    LetterResponseReceived,

    // Public Portal
    CaseStatusChanged,
    LetterAvailableForDownload,
    SignatureRequired,

    // System
    SystemAnnouncement,
    DataImportCompleted
}
```

### 10.2 Notification Triggers

| Trigger Event | Recipients | Channel |
|--------------|-----------|---------|
| Workflow step assigned | Assigned officer | In-app + Email |
| Workflow step overdue (configurable days) | Assigned officer + Manager | In-app + Email |
| Letter generated, ready for signing | Regional Manager | In-app + Email |
| Letter 1 issued | Public user (water user) | Email + Portal |
| Letter response overdue (7 days before due) | Assigned officer | In-app |
| Letter response overdue (on due date) | Assigned officer + Manager | In-app + Email |
| Letter response received | Assigned officer | In-app + Email |
| Letter 3 issued (ELU confirmed) | Public user | Email + Portal |
| Case status changed | Public user | Email + Portal |
| Unregistered user identified | Regional Manager | In-app |

### 10.3 Background Job Schedule

```
[Hangfire / BackgroundService]

EVERY 15 MINUTES:
  - Check for new email notifications to send
  - Process email send queue

EVERY HOUR:
  - Check for overdue workflow steps (> configured SLA)
  - Generate overdue notifications for managers

DAILY AT 06:00:
  - Check for letters approaching due date (7 days warning)
  - Check for letters past due date
  - Generate daily summary for Project Managers
  - Generate daily summary for Regional Managers

WEEKLY (Monday 08:00):
  - Generate weekly progress report per catchment
  - Send to all managers
```

---

## 11. Electronic Signature Module

### 11.1 Architecture

```csharp
public interface ISignatureService
{
    // Request a signature from an internal user
    Task<SignatureRequest> RequestSignature(
        Guid documentId, Guid signerId, string reason);

    // Request signature from a public portal user (e.g., Letter 1 application)
    Task<SignatureRequest> RequestPublicSignature(
        Guid documentId, Guid publicUserId, string reason);

    // Capture and store signature
    Task<DigitalSignature> CaptureSignature(
        Guid signatureRequestId, byte[] signatureImage, string ipAddress);

    // Verify a signature
    Task<bool> VerifySignature(Guid signatureId);

    // Get pending signature requests for a user
    Task<ICollection<SignatureRequest>> GetPendingRequests(Guid userId, UserType type);
}

public class DigitalSignature
{
    public Guid SignatureId { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; }

    // Signer (one of these will be set)
    public Guid? ApplicationUserId { get; set; }
    public Guid? PublicUserId { get; set; }

    public byte[] SignatureImage { get; set; }      // PNG of drawn signature
    public string SignatureHash { get; set; }       // SHA-256 hash for integrity
    public DateTime SignedAt { get; set; }
    public string IPAddress { get; set; }
    public string UserAgent { get; set; }
    public string Reason { get; set; }              // "Section 35(4) Confirmation"

    // For legal validity
    public string DocumentHashAtSigning { get; set; } // Hash of document when signed
}

public class SignatureRequest
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public Guid? PublicUserId { get; set; }
    public string Reason { get; set; }
    public SignatureRequestStatus Status { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? DigitalSignatureId { get; set; }
}

public enum SignatureRequestStatus { Pending, Completed, Declined, Expired }
```

### 11.2 Signature Flow

```
[Letter Generated (PDF)]
         |
         v
[Signature Request Created]
         |
    +----+----+
    |         |
    v         v
Internal   Public Portal
User       User
    |         |
    v         v
[In-App     [Portal shows
 Signing     "Sign Letter"
 Page]       Page]
    |         |
    v         v
[SignaturePad.js - draw signature on canvas]
         |
         v
[Capture: signature image + timestamp + IP + document hash]
         |
         v
[Store DigitalSignature record]
         |
         v
[Embed signature image into PDF]
         |
         v
[Letter marked as Signed]
         |
         v
[Notification sent to next participant]
```

---

## 12. Public Portal - Water User Self-Service

### 12.1 Portal Architecture

The public portal is a **separate ASP.NET Core Area** within the same application, with its own controllers, views, and authentication pipeline.

```
/Portal
  /Account
    /Register          - Self-registration
    /Login             - Authentication
    /Profile           - Update contact details
  /Dashboard           - Overview of all linked properties/cases
  /Cases
    /{caseId}          - Detailed view of a single V&V case
    /{caseId}/Letters  - View/download all letters for this case
    /{caseId}/Sign     - Sign a letter electronically
    /{caseId}/Upload   - Upload supporting documents
  /Notifications       - View all notifications
```

### 12.2 Registration & Identity Verification Flow

```
[Water User visits /Portal/Register]
         |
         v
[Enters: Name, ID Number, Email, Phone, Property Ref (optional)]
         |
         v
[Email verification sent]
         |
         v
[User clicks verification link]
         |
         v
[Account created as PublicUser (Status = Pending)]
         |
         v
[SYSTEM: Attempt to match PublicUser to existing PropertyOwner]
         |
    +----+----+
    |         |
    v         v
[Match      [No match found]
 found]           |
    |             v
    v         [Manual review by
[Auto-link    DWS officer]
 PublicUser         |
 to Property-       v
 Owner]       [Officer links
    |          or creates
    v          PropertyOwner]
[Status =          |
 Active]           v
    |         [Status = Active]
    +----+----+
         |
         v
[User can now see their cases on Dashboard]
```

### 12.3 Public Portal - Dashboard Data Flow

```
[PublicUser logs in]
         |
         v
[Load linked PropertyOwner]
         |
         v
[Load PropertyOwnerships for this owner]
         |
         v
[For each Property:]
    +-- Load FileMaster (V&V status)
    +-- Load latest WorkflowInstance (current step)
    +-- Load LetterIssuances (correspondence history)
    +-- Load Validation (status)
         |
         v
[Render Dashboard]
  +--------------------------------------------------+
  | MY PROPERTIES                                     |
  |                                                   |
  | Property: Farm ABC 123/4                          |
  | Status: [In Verification - Step: GIS Analysis]    |
  | Last Updated: 2026-01-15                          |
  | Letters: 0 pending signature                      |
  | [View Details]                                    |
  |                                                   |
  | Property: Farm XYZ 567/8                          |
  | Status: [Section 35 - Letter 1 Issued]            |
  | Last Updated: 2026-02-01                          |
  | Letters: 1 pending signature                      |
  | [View Details] [Sign Letter]                      |
  +--------------------------------------------------+
```

### 12.4 Public Portal - Case Detail View

```
  +--------------------------------------------------+
  | CASE: Farm ABC 123/4 (Reg: W12345)               |
  +--------------------------------------------------+
  |                                                   |
  | PROPERTY DETAILS                                  |
  | Farm Name: ________    SG Code: ________          |
  | Quaternary: ________   WMA: ________              |
  | Property Size: ____ha  Coordinates: S__, E__      |
  |                                                   |
  | OWNER DETAILS                                     |
  | Name: ________         ID: ________               |
  | Email: ________        Phone: ________            |
  | [Update Contact Details]                          |
  |                                                   |
  | V&V STATUS TIMELINE                               |
  | [===========|--------->                     ]     |
  |  Inception   Verification    Validation            |
  |              ^current step                         |
  |                                                   |
  | Current Step: GIS Analysis & Field Digitising      |
  | Assigned To: J. Smith (DWS Regional Office)        |
  | Started: 2026-01-10                                |
  |                                                   |
  | CORRESPONDENCE                                     |
  | Date       | Letter              | Status          |
  | 2026-01-05 | Letter 1 (S35(1))   | Issued          |
  | 2026-02-01 | Response Due                          |
  | [Download Letter 1]  [Sign & Return]               |
  |                                                   |
  | DOCUMENTS                                          |
  | [Upload Supporting Document]                       |
  | - WARMS Report (uploaded by DWS)                   |
  | - GIS Map Qualify Period (uploaded by DWS)         |
  +--------------------------------------------------+
```

### 12.5 Security: Data Access Control

The public portal MUST enforce strict data isolation:

```csharp
// Every public portal query MUST be scoped to the authenticated user
public class PublicCaseService
{
    public async Task<CaseViewModel?> GetCase(Guid publicUserId, Guid fileMasterId)
    {
        // 1. Load the PublicUser and their linked PropertyOwner
        var publicUser = await _context.PublicUsers
            .Include(u => u.PropertyOwner)
            .FirstOrDefaultAsync(u => u.PublicUserId == publicUserId);

        if (publicUser?.PropertyOwnerId == null) return null;

        // 2. Verify the PropertyOwner owns the property linked to this FileMaster
        var fileMaster = await _context.FileMasters
            .Include(f => f.Property)
                .ThenInclude(p => p.PropertyOwnerships)
            .FirstOrDefaultAsync(f => f.Id == fileMasterId
                && f.Property.PropertyOwnerships
                    .Any(po => po.PropertyOwnerId == publicUser.PropertyOwnerId));

        if (fileMaster == null) return null; // Access denied - not their property

        // 3. Return only permitted data
        return MapToViewModel(fileMaster);
    }
}
```

---

## 13. External System Integration

### 13.1 Integration Architecture

```csharp
public interface IIntegrationService
{
    // WARMS Integration
    Task<WarmsPropertyData> GetWarmsRegistration(string registrationNumber);
    Task UpdateWarmsWithElu(Guid fileMasterId, EluResult eluResult);
    Task<ICollection<WarmsRecord>> GetWarmsAuditDelta(DateTime sinceDate);

    // eWULAAS Integration
    Task PrePopulateLicenceApplication(Guid fileMasterId);

    // Surveyor General (DRDLR)
    Task<SgPropertyData> GetSgPropertyData(string sgCode);

    // Deeds Office (DRDLR)
    Task<DeedsData> GetDeedsData(string titleDeedNumber);
}
```

### 13.2 Integration Data Flows

```
+------------------+          +------------------+
|   V&V System     |  <---->  |     WARMS        |
|                  |          |                  |
| FileMaster       | -------> | Registration     |
| (Reg Number)     |          | (Update ELU)     |
|                  | <------- |                  |
| (New records)    |          | (Delta extract)  |
+------------------+          +------------------+

+------------------+          +------------------+
|   V&V System     |  ------> |    eWULAAS       |
|                  |          |                  |
| ELU Result       | -------> | Licence App      |
| Property data    |          | (Pre-populated)  |
| Owner data       |          |                  |
+------------------+          +------------------+

+------------------+          +------------------+
|   V&V System     |  <------ | Surveyor General |
|                  |          |                  |
| Property.SGCode  | -------> | SG Diagram       |
| (query)          |          | Farm boundaries  |
|                  | <------- | Subdivisions     |
+------------------+          +------------------+

+------------------+          +------------------+
|   V&V System     |  <------ |  Deeds Office    |
|                  |          |                  |
| TitleDeedNumber  | -------> | Owner info       |
| (query)          |          | Title deed data  |
|                  | <------- | Transfer history |
+------------------+          +------------------+
```

### 13.3 Integration Patterns

| Integration | Pattern | Frequency | Direction |
|------------|---------|-----------|-----------|
| WARMS - Delta Extract | Scheduled batch (API or file) | Daily | WARMS -> V&V |
| WARMS - ELU Update | Real-time API call on Letter 3 issuance | Event-driven | V&V -> WARMS |
| eWULAAS - Pre-populate | Real-time API call | On demand | V&V -> eWULAAS |
| Surveyor General | API query | On demand (CP09) | SG -> V&V |
| Deeds Office | API query | On demand (CP09) | Deeds -> V&V |

---

## 14. Reporting & Audit Trail

### 14.1 Audit Trail

```csharp
public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public DateTime Timestamp { get; set; }

    // Who
    public Guid? ApplicationUserId { get; set; }
    public Guid? PublicUserId { get; set; }
    public string UserName { get; set; }

    // What
    public string EntityType { get; set; }      // "FileMaster", "LetterIssuance", etc.
    public string EntityId { get; set; }
    public AuditAction Action { get; set; }     // Create, Update, Delete, View, Sign, etc.
    public string? OldValues { get; set; }      // JSON
    public string? NewValues { get; set; }      // JSON

    // Context
    public string? Description { get; set; }
    public string IPAddress { get; set; }
    public string? WorkflowStepContext { get; set; }
}

public enum AuditAction
{
    Create, Read, Update, Delete,
    Login, Logout,
    WorkflowStepStarted, WorkflowStepCompleted,
    LetterGenerated, LetterSigned, LetterIssued,
    SignatureCaptured,
    DocumentUploaded, DocumentDownloaded,
    IntegrationSent, IntegrationReceived
}
```

### 14.2 Project Control Reports

Per the V&V Guide Appendix C, the system must produce:

| Report | Content | Frequency |
|--------|---------|-----------|
| **Catchment Progress** | Total records, status breakdown per CP, completion % | Weekly |
| **Letter Tracking** | Letters issued, responses received, overdue, RTS | Weekly |
| **Validation Summary** | Properties validated, ELU volumes determined | Monthly |
| **User Activity** | Actions per officer, cases completed | Monthly |
| **Public Portal Usage** | Registrations, logins, documents signed | Monthly |
| **Integration Health** | WARMS sync status, errors, pending updates | Daily |

### 14.3 Dashboard Metrics (existing + new)

**Internal Dashboard (existing - enhance):**
- Total Validations (complete, in-progress, not started)
- ELU Estimates (total volume, by catchment)
- Outstanding Validations (by step, by officer)
- Investigations (unlawful use cases)
- **NEW:** Letter tracking summary
- **NEW:** Overdue tasks count
- **NEW:** Public portal activity

**Public Portal Dashboard:**
- My Properties count
- Cases with pending actions
- Letters awaiting signature
- Latest notifications

---

## 15. Data Flow Diagrams

### 15.1 End-to-End System Data Flow

```
                                    EXTERNAL SYSTEMS
                         +----------------------------------+
                         | WARMS | eWULAAS | SG | Deeds    |
                         +---+------+--------+------+-------+
                             |      |        |      |
                             v      v        v      v
+==================================================================+
|                    INTEGRATION SERVICE LAYER                      |
+==================================================================+
         |                    |                    |
         v                    v                    v
+------------------+  +----------------+  +------------------+
| PHASE 1:         |  | PHASE 2:       |  | PHASE 3:         |
| PROJECT INCEPTION|  | VERIFICATION   |  | VALIDATION       |
|                  |  | (per property) |  | (Section 35)     |
| CP01-CP05        |  | CP06-CP16      |  | CP17-CP21        |
+--------+---------+  +-------+--------+  +--------+---------+
         |                    |                    |
         v                    v                    v
+==================================================================+
|                     WORKFLOW ENGINE                                |
|  (State machine managing transitions between control points)      |
+==================================================================+
         |                    |                    |
         v                    v                    v
+------------------+  +----------------+  +------------------+
| CALCULATOR       |  | LETTER         |  | NOTIFICATION     |
| ENGINE           |  | SERVICE        |  | ENGINE           |
| - SAPWAT         |  | - Generate     |  | - In-app         |
| - Dam Volumes    |  | - Track        |  | - Email          |
| - ELU            |  | - Batch        |  | - Reminders      |
| - SFRA           |  |                |  |                  |
+--------+---------+  +-------+--------+  +--------+---------+
         |                    |                    |
         v                    v                    v
+==================================================================+
|                     SERVICE LAYER                                 |
|  PropertyService | ValidationService | FileMasterService |        |
|  ForestationService | IrrigationService | StoringService |        |
+==================================================================+
         |                    |                    |
         v                    v                    v
+==================================================================+
|                    REPOSITORY LAYER                               |
|  PropertyRepo | AddressRepo | ForestationRepo | FieldAndCropRepo |
|  FileMasterRepo | ValidationRepo | LetterRepo | etc.             |
+==================================================================+
         |
         v
+==================================================================+
|                SQL SERVER DATABASE                                |
|  +------------------+  +------------------+  +-----------------+ |
|  | Core Domain      |  | Workflow         |  | Public Portal   | |
|  | - Properties     |  | - WorkflowInst   |  | - PublicUsers   | |
|  | - PropertyOwners |  | - WorkflowSteps  |  | - Notifications | |
|  | - FileMasters    |  | - AuditLog       |  | - SignatureReqs | |
|  | - Irrigation     |  |                  |  |                 | |
|  | - Storing        |  | Letters          |  | Documents       | |
|  | - Forestation    |  | - LetterIssuance |  | - Document      | |
|  | - FieldAndCrop   |  | - DigitalSig     |  | - DocumentBlob  | |
|  | - DamCalculation |  |                  |  |                 | |
|  | - Entitlement    |  | Reference        |  |                 | |
|  | - Validation     |  | - Crops, Rivers  |  |                 | |
|  | - WaterSource    |  | - Lookup tables  |  |                 | |
|  +------------------+  +------------------+  +-----------------+ |
+==================================================================+
         ^                    ^
         |                    |
+--------+---------+  +-------+--------+
| INTERNAL APP     |  | PUBLIC PORTAL   |
| (DWS/CMA Users)  |  | (Water Users)   |
| MVC Controllers  |  | Portal Contrlrs |
| Razor Views      |  | Razor Pages     |
+------------------+  +------------------+
```

### 15.2 Per-Property Lifecycle Data Flow

```
[Property enters V&V system]
         |
         v
[FileMaster created (CP01 or CP02)]
         |
         v
[WorkflowInstance created, Phase=INCEPTION]
         |
         v
[CP06: WARMS evaluation]
    |-- READ: WARMS API
    |-- WRITE: FileMaster, Irrigation, Storing, Forestation
         |
         v
[CP07-08: Spatial + Supportive info]
    |-- READ: SG API, Deeds API, GIS Service
    |-- WRITE: SateliteImage, Property, FileMaster
         |
         v
[CP09: Property & Source evaluation]
    |-- READ: Title deeds, SG diagram
    |-- WRITE: Property, PropertyOwner, PropertyOwnership, WaterSource
         |
         v
[CP10: Additional information]
    |-- READ: Permits, approvals, court decisions
    |-- WRITE: AdditionalInformation, Entitlement
         |
         v
[CP11: GIS Analysis]
    |-- READ: Satellite imagery, property boundaries
    |-- WRITE: FieldAndCrop (areas), DamCalculation (identified)
         |
         v
[CP12: SAPWAT Modelling] >>> CALCULATOR ENGINE
    |-- READ: FieldAndCrop, Crop, IrrigationSystem
    |-- WRITE: FieldAndCrop.SAPWATCalculationResult
         |
         v
[CP13: ELU Calculation] >>> CALCULATOR ENGINE
    |-- READ: All verification data
    |-- WRITE: Irrigation (all scenarios), Entitlement (volume)
         |
    +----+----+
    |         |
    v         v
[CP15: Dams] [CP16: SFRA] >>> CALCULATOR ENGINE
    |         |
    v         v
DamCalc    Forestation
Storing    (populated)
    |         |
    +----+----+
         |
         v
[CP14: Update V&V Database]
    |-- WRITE: FileMaster.ValidationStatus
    |-- WRITE: All tables finalized
         |
         v
=== VERIFICATION COMPLETE ===
         |
         v
[CP17-18: Public Participation + Field Visits]
    |-- CREATE: Letter 1 (LetterIssuance)
    |-- TRIGGER: NotificationService (public user)
    |-- TRIGGER: SignatureService (if public portal)
         |
         v
[Section 35 Letter Tracking State Machine]
    |-- Letter 1 -> Response -> Letter 2/3
    |-- Escalation path: L1A, L2A
    |-- WRITE: LetterIssuance (response tracking)
         |
         v
[Letter 3 Issued: ELU Confirmed]
    |-- TRIGGER: WARMS update (IntegrationService)
    |-- TRIGGER: eWULAAS pre-populate (if compulsory licensing)
    |-- TRIGGER: Notification to public user
    |-- WRITE: FileMaster status = VALIDATED
         |
    +----+----+
    |         |
    v         v
[Lawful]  [Unlawful]
    |         |
    v         v
 DONE     Letter 4A -> Letter 4&5
          (enforcement)
```

---

## 16. Component Inventory & Build Status

### 16.1 Current State vs Required Components

| Component | Status | Priority | Effort |
|-----------|--------|----------|--------|
| **Models** | | | |
| Core domain models (Property, Owner, etc.) | BUILT | - | - |
| FileMaster | BUILT | - | - |
| WorkflowInstance + WorkflowStepRecord | NOT BUILT | P0 | Medium |
| PublicUser | NOT BUILT | P1 | Small |
| Enhanced LetterIssuance | PARTIAL | P0 | Medium |
| Document (file storage) | NOT BUILT | P0 | Small |
| DigitalSignature + SignatureRequest | NOT BUILT | P1 | Medium |
| Notification | NOT BUILT | P1 | Small |
| AuditLog | NOT BUILT | P1 | Small |
| AdditionalInformation | NOT BUILT | P2 | Small |
| **Interfaces** | | | |
| IPropertyInterface | BUILT | - | - |
| IAddress | BUILT | - | - |
| IEntitlement (has naming bug) | BUILT (buggy) | P0 fix | Tiny |
| IForestation | BUILT | - | - |
| IFieldAndCrop | BUILT | - | - |
| IDamCalculation | BUILT | - | - |
| IWorkflowEngine | NOT BUILT | P0 | Medium |
| ICalculatorEngine | NOT BUILT | P0 | Large |
| ILetterService | NOT BUILT | P0 | Medium |
| INotificationService | NOT BUILT | P1 | Medium |
| ISignatureService | NOT BUILT | P1 | Medium |
| IIntegrationService | NOT BUILT | P2 | Large |
| IAuditService | NOT BUILT | P1 | Small |
| IReportingService | NOT BUILT | P2 | Medium |
| IPublicCaseService | NOT BUILT | P1 | Medium |
| **Repositories** | | | |
| PropertyRepository | BUILT (partial) | P0 complete | Small |
| AddressRepository | BUILT | - | - |
| ForestationRepository | BUILT | - | - |
| FileMasterRepository | NOT BUILT | P0 | Medium |
| WorkflowRepository | NOT BUILT | P0 | Medium |
| LetterIssuanceRepository | NOT BUILT | P0 | Medium |
| Remaining repositories | NOT BUILT | P1 | Medium |
| **Services** | | | |
| PropertyService | EMPTY | P0 | Medium |
| WorkflowEngine | NOT BUILT | P0 | Large |
| CalculatorEngine | NOT BUILT | P0 | Large |
| LetterService | NOT BUILT | P0 | Large |
| NotificationService | NOT BUILT | P1 | Medium |
| SignatureService | NOT BUILT | P1 | Medium |
| IntegrationService | NOT BUILT | P2 | Large |
| AuditService | NOT BUILT | P1 | Small |
| ReportingService | NOT BUILT | P2 | Medium |
| PublicCaseService | NOT BUILT | P1 | Medium |
| **Controllers** | | | |
| HomeController | BUILT | - | - |
| PropertyController | BUILT (basic) | P0 enhance | Medium |
| OwnerController | BUILT (basic) | P1 enhance | Medium |
| ValidationController | BUILT (basic) | P0 enhance | Medium |
| WorkflowController | NOT BUILT | P0 | Medium |
| LetterController | NOT BUILT | P0 | Medium |
| FileMasterController | NOT BUILT | P0 | Medium |
| GISController | NOT BUILT | P2 | Medium |
| ReportController | NOT BUILT | P2 | Medium |
| Portal/AccountController | NOT BUILT | P1 | Medium |
| Portal/DashboardController | NOT BUILT | P1 | Medium |
| Portal/CaseController | NOT BUILT | P1 | Medium |
| **Views** | | | |
| Home/Dashboard | BUILT | P1 enhance | Small |
| Property CRUD | BUILT (partial) | P0 complete | Medium |
| Owner CRUD | BUILT (partial) | P1 complete | Medium |
| Validation list | BUILT (basic) | P0 enhance | Medium |
| Workflow views | NOT BUILT | P0 | Large |
| FileMaster views | NOT BUILT | P0 | Large |
| Letter management views | NOT BUILT | P0 | Large |
| Field & Crop views | NOT BUILT | P0 | Medium |
| Dam Calculation views | NOT BUILT | P1 | Medium |
| Storing views | NOT BUILT | P1 | Medium |
| Forestation views | NOT BUILT | P1 | Medium |
| Report views | NOT BUILT | P2 | Medium |
| Portal views (all) | NOT BUILT | P1 | Large |

### 16.2 Known Issues to Fix

| Issue | File | Description |
|-------|------|-------------|
| Interface naming | `Interfaces/IEntitlement.cs` | `public interface Entitlement` should be `public interface IEntitlement` |
| Deleted model refs | `DatabaseContexts/ApplicationDBContext.cs` | References `AuthorisationType` and `PropertyAddress` which are deleted |
| Typo in model | `Models/FieldAndCrop.cs` | `Entitlemwnt` should be `Entitlement` |
| View filename typo | `Views/Property/PropertRegister.cshtml` | Should be `PropertyRegister.cshtml` |
| Hardcoded view data | Multiple views | Views have hardcoded sample data instead of model binding |
| Empty service | `Services/PropertyService.cs` | Placeholder, no implementation |
| NotImplementedException | `Repositories/PropertyRepository.cs` | `TransferOwnership()` and `ListPropertyByOwner()` throw |

---

## 17. Database Design Considerations

### 17.1 Schema Organization

```
SCHEMA: dbo (default - core domain)
  - Properties, PropertyOwners, PropertyOwnerships
  - Addresses, Entitlements, EntitlementTypes
  - FileMasters, Validations
  - Irrigations, Storings, Forestations
  - FieldAndCrops, DamCalculations
  - WaterSources, Rivers, Crops, CropTypes
  - IrrigationSystems, IrrigationBoards
  - GovernmentWaterControlAreas, GovernmentWaterSchemes
  - SateliteImages, Periods, CustomerTypes

SCHEMA: workflow
  - WorkflowInstances, WorkflowStepRecords

SCHEMA: correspondence
  - LetterIssuances, LetterTypes, IssuedLetters
  - Documents, DocumentBlobs

SCHEMA: signature
  - DigitalSignatures, SignatureRequests

SCHEMA: portal
  - PublicUsers

SCHEMA: system
  - Notifications, AuditLogs

SCHEMA: identity (ASP.NET Identity tables)
  - AspNetUsers, AspNetRoles, AspNetUserRoles, etc.
```

### 17.2 Data Volume Estimates

| Table | Estimated Records | Growth Rate |
|-------|------------------|-------------|
| FileMaster | 500,000 | Low (batch imports) |
| Properties | 500,000 | Low |
| PropertyOwners | 300,000 | Low |
| FieldAndCrop | 2,500,000 (5 per property avg) | Medium |
| DamCalculation | 200,000 | Low |
| Irrigation | 500,000 | Low |
| Storing | 200,000 | Low |
| Forestation | 100,000 | Low |
| LetterIssuance | 750,000 (avg 1.5 per file) | High during S35 |
| WorkflowInstance | 500,000 | Matches FileMaster |
| WorkflowStepRecord | 10,000,000 (20 steps x 500k) | High |
| AuditLog | 50,000,000+ | Very High |
| PublicUsers | 125,000 | Medium |
| Notifications | 5,000,000+ | High |

### 17.3 Indexing Strategy

Key indexes to create beyond primary keys:

```sql
-- FileMaster lookups
CREATE INDEX IX_FileMaster_RegistrationNumber ON FileMasters(RegistrationNumber);
CREATE INDEX IX_FileMaster_PropertyId ON FileMasters(PropertyId);
CREATE INDEX IX_FileMaster_ValidationStatus ON FileMasters(GetValidationStatus);

-- Property lookups
CREATE INDEX IX_Property_SGCode ON Properties(SGCode);
CREATE INDEX IX_Property_QuatenaryDrainage ON Properties(QuatenaryDrainage);

-- Workflow lookups
CREATE INDEX IX_WorkflowInstance_Status ON workflow.WorkflowInstances(Status, CurrentStep);
CREATE INDEX IX_WorkflowInstance_AssignedTo ON workflow.WorkflowInstances(AssignedToId);

-- Letter tracking
CREATE INDEX IX_LetterIssuance_FileMasterId ON correspondence.LetterIssuances(FileMasterId);
CREATE INDEX IX_LetterIssuance_ResponseStatus ON correspondence.LetterIssuances(ResponseStatus);
CREATE INDEX IX_LetterIssuance_DueDate ON correspondence.LetterIssuances(DueDate) WHERE ResponseStatus = 1;

-- Public portal
CREATE INDEX IX_PublicUser_PropertyOwnerId ON portal.PublicUsers(PropertyOwnerId);
CREATE INDEX IX_Notification_UserId ON system.Notifications(ApplicationUserId, IsRead);
CREATE INDEX IX_Notification_PublicUserId ON system.Notifications(PublicUserId, IsRead);

-- Audit (partitioned by month for performance)
CREATE INDEX IX_AuditLog_Entity ON system.AuditLogs(EntityType, EntityId);
CREATE INDEX IX_AuditLog_Timestamp ON system.AuditLogs(Timestamp);
```

---

## 18. Implementation Roadmap

### Phase A: Foundation (Weeks 1-4)

1. Fix known bugs (interface naming, deleted model refs, typos)
2. Build `WorkflowInstance` + `WorkflowStepRecord` models and migrations
3. Build `Document` model for file storage
4. Build `AuditLog` model and `IAuditService`
5. Complete `PropertyService` and all remaining repositories
6. Build `FileMasterController` + views (CRUD + detail views)
7. Register all services in DI (`Program.cs`)

### Phase B: Workflow Engine (Weeks 5-8)

1. Build `IWorkflowEngine` service with state machine logic
2. Implement CP01-CP05 (Phase 1: Inception) workflow
3. Implement CP06-CP16 (Phase 2: Verification) workflow
4. Build `WorkflowController` + views (dashboard, step management)
5. Build validation rules (gate conditions between steps)
6. Add workflow status to existing Property and Validation views

### Phase C: Calculator Engine (Weeks 9-12)

1. Build SAPWAT calculator (field & crop modelling)
2. Build Dam Volume calculator
3. Build ELU determination calculator (all scenarios)
4. Build SFRA calculator
5. Build `FieldAndCrop` views + forms (qualify and current period)
6. Build `DamCalculation` views + forms
7. Build `Storing` views + forms
8. Build `Forestation` views + forms
9. Integrate calculators into workflow steps CP12, CP13, CP15, CP16

### Phase D: Letter & Correspondence Engine (Weeks 13-16)

1. Build letter template system (QuestPDF)
2. Build `ILetterService` with all 7 letter types
3. Build Letter tracking state machine (Section 35 flow)
4. Build `LetterController` + views (generate, track, batch)
5. Build correspondence history views per property
6. Build batch letter generation for catchment-wide mailings

### Phase E: Notification & Signature (Weeks 17-20)

1. Build `Notification` model and `INotificationService`
2. Build email sending (SMTP/SendGrid integration)
3. Build in-app notification UI (bell icon, notification drawer)
4. Build background jobs for reminders and overdue detection
5. Build `DigitalSignature` + `SignatureRequest` models
6. Build `ISignatureService` with SignaturePad.js frontend
7. Integrate signatures into letter workflow

### Phase F: Public Portal (Weeks 21-26)

1. Build `PublicUser` model and Identity configuration
2. Build registration flow with email verification
3. Build identity matching (PublicUser -> PropertyOwner)
4. Build Portal/Dashboard (property list, status overview)
5. Build Portal/Case detail view (timeline, letters, documents)
6. Build Portal/Sign letter (electronic signature on portal)
7. Build Portal/Upload documents
8. Build Portal/Notifications view
9. Security hardening and penetration testing

### Phase G: Integration & Reporting (Weeks 27-32)

1. WARMS integration (delta extract + ELU update)
2. eWULAAS integration (pre-populate licence applications)
3. Surveyor General / Deeds Office integration (if APIs available)
4. Build reporting service
5. Build report views (catchment progress, letter tracking, etc.)
6. Enhance dashboard with real data from all modules
7. Data migration tools for existing MS Access V&V databases

### Phase H: Stabilisation & Deployment (Weeks 33-36)

1. End-to-end testing with migrated data
2. Performance optimisation (indexing, query tuning)
3. User acceptance testing with DWS regional offices
4. Training materials and sessions
5. DWS IT environment deployment
6. Production go-live and monitoring

---

## Appendix A: New Models Summary

The following new models need to be created:

| Model | Purpose | Schema |
|-------|---------|--------|
| `WorkflowInstance` | Tracks V&V workflow per property | workflow |
| `WorkflowStepRecord` | Individual step completion records | workflow |
| `Document` | Stores uploaded/generated files | correspondence |
| `DigitalSignature` | Signature records with legal validity | signature |
| `SignatureRequest` | Pending signature requests | signature |
| `PublicUser` | Public portal user accounts | portal |
| `Notification` | In-app + email notification queue | system |
| `AuditLog` | Immutable action log | system |
| `AdditionalInformation` | Permits, approvals, court decisions per property | dbo |

## Appendix B: Enhanced Existing Models

| Model | Changes Needed |
|-------|---------------|
| `FileMaster` | Add `ICollection<LetterIssuance>`, `ICollection<Document>`, FK to `WorkflowInstance` |
| `LetterIssuance` | Major expansion - see Section 7.2 |
| `Validation` | Add FK to `FileMaster`, `ApplicationUser AssignedTo` |
| `Property` | Add `ICollection<SateliteImage>` navigation |
| `ApplicationUser` | Add role claims for the 7 internal roles |

## Appendix C: Service Interface Summary

| Interface | Key Methods |
|-----------|------------|
| `IWorkflowEngine` | `CreateWorkflow()`, `AdvanceStep()`, `GetCurrentStep()`, `CanAdvance()`, `GetBlockingIssues()` |
| `ICalculatorEngine` | `CalculateSapwat()`, `CalculateElu()`, `CalculateDamVolume()`, `CalculateSfra()` |
| `ILetterService` | `GenerateLetter()`, `GenerateLetterBatch()`, `SubmitForSigning()`, `IssueLetter()`, `RecordResponse()` |
| `INotificationService` | `SendToUser()`, `SendToPublicUser()`, `ProcessOverdueReminders()`, `GetUnread()` |
| `ISignatureService` | `RequestSignature()`, `CaptureSignature()`, `VerifySignature()`, `GetPendingRequests()` |
| `IIntegrationService` | `GetWarmsRegistration()`, `UpdateWarmsWithElu()`, `PrePopulateLicenceApplication()` |
| `IAuditService` | `LogAction()`, `GetAuditTrail()`, `GetEntityHistory()` |
| `IReportingService` | `GetCatchmentProgress()`, `GetLetterTrackingReport()`, `GetValidationSummary()` |
| `IPublicCaseService` | `GetCase()`, `GetCasesForUser()`, `UpdateContactDetails()`, `GetLettersForCase()` |
