using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDBContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> dbContextOption) : base(dbContextOption)
    { }

    // ── Core domain ──
    public DbSet<Property> Properties { get; set; }
    public DbSet<FileMaster> FileMasters { get; set; }
    public DbSet<Entitlement> Entitlements { get; set; }
    public DbSet<EntitlementType> EntitlementTypes { get; set; }
    public DbSet<Validation> Validations { get; set; }

    // ── Water use data ──
    public DbSet<Irrigation> Irrigations { get; set; }
    public DbSet<FieldAndCrop> FieldAndCrops { get; set; }
    public DbSet<Forestation> Forestations { get; set; }
    public DbSet<Storing> Storings { get; set; }
    public DbSet<DamCalculation> DamCalculations { get; set; }

    // ── Ownership ──
    public DbSet<PropertyOwner> PropertyOwners { get; set; }
    public DbSet<PropertyOwnership> PropertyOwnerships { get; set; }
    public DbSet<CustomerType> CustomerTypes { get; set; }

    // ── Reference / lookup tables ──
    public DbSet<Crop> Crops { get; set; }
    public DbSet<CropType> CropTypes { get; set; }
    public DbSet<Period> Periods { get; set; }
    public DbSet<WaterSource> WaterSources { get; set; }
    public DbSet<IrrigationSystem> IrrigationSystems { get; set; }
    public DbSet<River> Rivers { get; set; }
    public DbSet<GovernmentWaterControlArea> GovernmentWaterControlAreas { get; set; }
    public DbSet<GovernmentWaterScheme> GovernmentWaterSchemes { get; set; }
    public DbSet<IrrigationBoard> IrrigationBoards { get; set; }
    public DbSet<AuthorisationType> AuthorisationTypes { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<SateliteImage> SateliteImages { get; set; }

    // ── Authorisation ──
    public DbSet<Authorisation> Authorisations { get; set; }

    // ── GWCA proclamation rules ──
    public DbSet<GwcaProclamationRule> GwcaProclamationRules { get; set; }

    // ── Mapbooks (processed GIS map products) ──
    public DbSet<Mapbook> Mapbooks { get; set; }
    public DbSet<MapbookImage> MapbookImages { get; set; }

    // ── Organisational hierarchy ──
    public DbSet<Province> Provinces { get; set; }
    public DbSet<WaterManagementArea> WaterManagementAreas { get; set; }
    public DbSet<CatchmentArea> CatchmentAreas { get; set; }
    public DbSet<OrganisationalUnit> OrganisationalUnits { get; set; }

    // ── Workflow ──
    public DbSet<WorkflowState> WorkflowStates { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepRecord> WorkflowStepRecords { get; set; }

    // ── Letters & signatures ──
    public DbSet<LetterType> LetterTypes { get; set; }
    public DbSet<LetterIssuance> LetterIssuances { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DigitalSignature> DigitalSignatures { get; set; }
    public DbSet<SignatureRequest> SignatureRequests { get; set; }

    // ── Notifications ──
    public DbSet<Notification> Notifications { get; set; }

    // ── Audit ──
    public DbSet<AuditLog> AuditLogs { get; set; }

    // ── Public portal ──
    public DbSet<PublicUser> PublicUsers { get; set; }
    public DbSet<PublicUserProperty> PublicUserProperties { get; set; }
    public DbSet<CaseComment> CaseComments { get; set; }
    public DbSet<Objection> Objections { get; set; }
    public DbSet<ObjectionDocument> ObjectionDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Primary keys ──
        modelBuilder.Entity<Property>().HasKey(e => e.PropertyId);
        modelBuilder.Entity<FileMaster>().HasKey(e => e.FileMasterId);
        modelBuilder.Entity<Entitlement>().HasKey(e => e.EntitlementId);
        modelBuilder.Entity<EntitlementType>().HasKey(e => e.EntitlementTypeId);
        modelBuilder.Entity<Validation>().HasKey(e => e.ValidationId);

        modelBuilder.Entity<Irrigation>().HasKey(e => e.IrrigationId);
        modelBuilder.Entity<FieldAndCrop>().HasKey(e => e.FieldAndCropId);
        modelBuilder.Entity<Forestation>().HasKey(e => e.ForestationId);
        modelBuilder.Entity<Storing>().HasKey(e => e.StoringId);
        modelBuilder.Entity<DamCalculation>().HasKey(e => e.DamCalculationId);

        modelBuilder.Entity<PropertyOwner>().HasKey(e => e.OwnerId);
        modelBuilder.Entity<PropertyOwnership>().HasKey(e => e.Id);
        modelBuilder.Entity<CustomerType>().HasKey(e => e.Id);

        modelBuilder.Entity<Crop>().HasKey(e => e.CropId);
        modelBuilder.Entity<CropType>().HasKey(e => e.CropTypeId);
        modelBuilder.Entity<Period>().HasKey(e => e.PeriodId);
        modelBuilder.Entity<WaterSource>().HasKey(e => e.WaterSourceId);
        modelBuilder.Entity<IrrigationSystem>().HasKey(e => e.IrrigationSystemId);
        modelBuilder.Entity<River>().HasKey(e => e.RiverId);
        modelBuilder.Entity<GovernmentWaterControlArea>().HasKey(e => e.WaterControlAreaId);
        modelBuilder.Entity<GovernmentWaterScheme>().HasKey(e => e.WaterSchemeId);
        modelBuilder.Entity<IrrigationBoard>().HasKey(e => e.IrrigationBoardId);
        modelBuilder.Entity<AuthorisationType>().HasKey(e => e.AuthorisationTypeId);
        modelBuilder.Entity<Address>().HasKey(e => e.AddressId);
        modelBuilder.Entity<SateliteImage>().HasKey(e => e.ImageId);

        modelBuilder.Entity<Authorisation>().HasKey(e => e.AuthorisationId);

        modelBuilder.Entity<GwcaProclamationRule>().HasKey(e => e.RuleId);
        modelBuilder.Entity<Mapbook>().HasKey(e => e.MapbookId);
        modelBuilder.Entity<MapbookImage>().HasKey(e => e.MapbookImageId);

        modelBuilder.Entity<Province>().HasKey(e => e.ProvinceId);
        modelBuilder.Entity<WaterManagementArea>().HasKey(e => e.WmaId);
        modelBuilder.Entity<CatchmentArea>().HasKey(e => e.CatchmentAreaId);
        modelBuilder.Entity<OrganisationalUnit>().HasKey(e => e.OrgUnitId);

        modelBuilder.Entity<WorkflowState>().HasKey(e => e.WorkflowStateId);
        modelBuilder.Entity<WorkflowInstance>().HasKey(e => e.WorkflowInstanceId);
        modelBuilder.Entity<WorkflowStepRecord>().HasKey(e => e.WorkflowStepRecordId);

        modelBuilder.Entity<LetterType>().HasKey(e => e.LetterTypeId);
        modelBuilder.Entity<LetterIssuance>().HasKey(e => e.LetterIssuanceId);
        modelBuilder.Entity<Document>().HasKey(e => e.DocumentId);
        modelBuilder.Entity<DigitalSignature>().HasKey(e => e.SignatureId);
        modelBuilder.Entity<SignatureRequest>().HasKey(e => e.Id);

        modelBuilder.Entity<Notification>().HasKey(e => e.NotificationId);
        modelBuilder.Entity<AuditLog>().HasKey(e => e.AuditLogId);

        modelBuilder.Entity<PublicUser>().HasKey(e => e.PublicUserId);
        modelBuilder.Entity<PublicUserProperty>().HasKey(e => e.Id);
        modelBuilder.Entity<CaseComment>().HasKey(e => e.CommentId);
        modelBuilder.Entity<Objection>().HasKey(o => o.ObjectionId);
        modelBuilder.Entity<ObjectionDocument>().HasKey(od => od.Id);

        // ── Relationships ──

        // Property → Address
        modelBuilder.Entity<Property>()
            .HasOne(p => p.Address)
            .WithMany()
            .HasForeignKey(p => p.AddressId)
            .OnDelete(DeleteBehavior.SetNull);

        // Property → WMA
        modelBuilder.Entity<Property>()
            .HasOne(p => p.WaterManagementArea)
            .WithMany()
            .HasForeignKey(p => p.WmaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Property self-referencing (subdivision/consolidation lineage)
        modelBuilder.Entity<Property>()
            .HasOne(p => p.ParentProperty)
            .WithMany(p => p.ChildProperties)
            .HasForeignKey(p => p.ParentPropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // PropertyOwnership many-to-many
        modelBuilder.Entity<PropertyOwnership>()
            .HasOne(po => po.Property)
            .WithMany(p => p.PropertyOwnerships)
            .HasForeignKey(po => po.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PropertyOwnership>()
            .HasOne(po => po.PropertyOwner)
            .WithMany(po => po.PropertyOwnerships)
            .HasForeignKey(po => po.PropertyOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // FileMaster → Property
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.Property)
            .WithMany(p => p.FileMasters)
            .HasForeignKey(fm => fm.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // FileMaster → OrgUnit
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.OrgUnit)
            .WithMany()
            .HasForeignKey(fm => fm.OrgUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // FileMaster → Validator (ApplicationUser)
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.Validator)
            .WithMany()
            .HasForeignKey(fm => fm.ValidatorId)
            .OnDelete(DeleteBehavior.SetNull);

        // FileMaster → CapturePerson (ApplicationUser)
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.CapturePerson)
            .WithMany()
            .HasForeignKey(fm => fm.CapturePersonId)
            .OnDelete(DeleteBehavior.SetNull);

        // FileMaster → Entitlement
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.Entitlement)
            .WithMany()
            .HasForeignKey(fm => fm.EntitlementId)
            .OnDelete(DeleteBehavior.SetNull);

        // Authorisation → FileMaster
        modelBuilder.Entity<Authorisation>()
            .HasOne(a => a.FileMaster)
            .WithMany(fm => fm.Authorisations)
            .HasForeignKey(a => a.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Authorisation → AuthorisationType
        modelBuilder.Entity<Authorisation>()
            .HasOne(a => a.AuthorisationType)
            .WithMany()
            .HasForeignKey(a => a.AuthorisationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // LetterIssuance → FileMaster
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.FileMaster)
            .WithMany(fm => fm.LetterIssuances)
            .HasForeignKey(li => li.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // LetterIssuance → LetterType
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.LetterType)
            .WithMany()
            .HasForeignKey(li => li.LetterTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // LetterIssuance → PropertyOwner
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.PropertyOwner)
            .WithMany()
            .HasForeignKey(li => li.PropertyOwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // LetterIssuance → SignedBy (ApplicationUser)
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.SignedBy)
            .WithMany()
            .HasForeignKey(li => li.SignedById)
            .OnDelete(DeleteBehavior.SetNull);

        // LetterIssuance → Document
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.Document)
            .WithMany()
            .HasForeignKey(li => li.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        // LetterIssuance → DigitalSignature
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.DigitalSignature)
            .WithMany()
            .HasForeignKey(li => li.DigitalSignatureId)
            .OnDelete(DeleteBehavior.SetNull);

        // LetterIssuance self-referencing (reissue chain)
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.ReissuedFrom)
            .WithMany()
            .HasForeignKey(li => li.ReissuedFromId)
            .OnDelete(DeleteBehavior.Restrict);

        // Document → FileMaster
        modelBuilder.Entity<Document>()
            .HasOne(d => d.FileMaster)
            .WithMany(fm => fm.Documents)
            .HasForeignKey(d => d.FileMasterId)
            .OnDelete(DeleteBehavior.SetNull);

        // Document → UploadedBy (ApplicationUser)
        modelBuilder.Entity<Document>()
            .HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Document → UploadedBy (PublicUser)
        modelBuilder.Entity<Document>()
            .HasOne(d => d.UploadedByPublicUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByPublicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // DigitalSignature → Document
        modelBuilder.Entity<DigitalSignature>()
            .HasOne(ds => ds.Document)
            .WithMany()
            .HasForeignKey(ds => ds.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // DigitalSignature → ApplicationUser
        modelBuilder.Entity<DigitalSignature>()
            .HasOne(ds => ds.ApplicationUser)
            .WithMany()
            .HasForeignKey(ds => ds.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // DigitalSignature → PublicUser
        modelBuilder.Entity<DigitalSignature>()
            .HasOne(ds => ds.PublicUser)
            .WithMany()
            .HasForeignKey(ds => ds.PublicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // SignatureRequest → Document
        modelBuilder.Entity<SignatureRequest>()
            .HasOne(sr => sr.Document)
            .WithMany()
            .HasForeignKey(sr => sr.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // SignatureRequest → ApplicationUser
        modelBuilder.Entity<SignatureRequest>()
            .HasOne(sr => sr.ApplicationUser)
            .WithMany()
            .HasForeignKey(sr => sr.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // SignatureRequest → PublicUser
        modelBuilder.Entity<SignatureRequest>()
            .HasOne(sr => sr.PublicUser)
            .WithMany()
            .HasForeignKey(sr => sr.PublicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // SignatureRequest → DigitalSignature
        modelBuilder.Entity<SignatureRequest>()
            .HasOne(sr => sr.DigitalSignature)
            .WithMany()
            .HasForeignKey(sr => sr.DigitalSignatureId)
            .OnDelete(DeleteBehavior.SetNull);

        // CaseComment → FileMaster
        modelBuilder.Entity<CaseComment>()
            .HasOne(cc => cc.FileMaster)
            .WithMany(fm => fm.CaseComments)
            .HasForeignKey(cc => cc.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // CaseComment → PublicUser
        modelBuilder.Entity<CaseComment>()
            .HasOne(cc => cc.PublicUser)
            .WithMany()
            .HasForeignKey(cc => cc.PublicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // CaseComment → ApplicationUser
        modelBuilder.Entity<CaseComment>()
            .HasOne(cc => cc.ApplicationUser)
            .WithMany()
            .HasForeignKey(cc => cc.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // CaseComment self-referencing (threading)
        modelBuilder.Entity<CaseComment>()
            .HasOne(cc => cc.ParentComment)
            .WithMany(cc => cc.Replies)
            .HasForeignKey(cc => cc.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Objection → FileMaster
        modelBuilder.Entity<Objection>()
            .HasOne(o => o.FileMaster)
            .WithMany()
            .HasForeignKey(o => o.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Objection → PublicUser
        modelBuilder.Entity<Objection>()
            .HasOne(o => o.PublicUser)
            .WithMany()
            .HasForeignKey(o => o.PublicUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ObjectionDocument → Objection
        modelBuilder.Entity<ObjectionDocument>()
            .HasOne(od => od.Objection)
            .WithMany(o => o.ObjectionDocuments)
            .HasForeignKey(od => od.ObjectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ObjectionDocument → Document
        modelBuilder.Entity<ObjectionDocument>()
            .HasOne(od => od.Document)
            .WithMany()
            .HasForeignKey(od => od.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification → FileMaster
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.FileMaster)
            .WithMany(fm => fm.Notifications)
            .HasForeignKey(n => n.FileMasterId)
            .OnDelete(DeleteBehavior.SetNull);

        // Notification → ApplicationUser
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.ApplicationUser)
            .WithMany()
            .HasForeignKey(n => n.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Notification → PublicUser
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.PublicUser)
            .WithMany()
            .HasForeignKey(n => n.PublicUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // PublicUserProperty → PublicUser
        modelBuilder.Entity<PublicUserProperty>()
            .HasOne(pup => pup.PublicUser)
            .WithMany(pu => pu.PublicUserProperties)
            .HasForeignKey(pup => pup.PublicUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // PublicUserProperty → Property
        modelBuilder.Entity<PublicUserProperty>()
            .HasOne(pup => pup.Property)
            .WithMany()
            .HasForeignKey(pup => pup.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // PublicUserProperty → ApprovedBy (ApplicationUser)
        modelBuilder.Entity<PublicUserProperty>()
            .HasOne(pup => pup.ApprovedByUser)
            .WithMany()
            .HasForeignKey(pup => pup.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // WorkflowInstance → FileMaster
        modelBuilder.Entity<WorkflowInstance>()
            .HasOne(wi => wi.FileMaster)
            .WithMany()
            .HasForeignKey(wi => wi.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowInstance → CurrentState
        modelBuilder.Entity<WorkflowInstance>()
            .HasOne(wi => wi.CurrentWorkflowState)
            .WithMany()
            .HasForeignKey(wi => wi.CurrentWorkflowStateId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowStepRecord → WorkflowInstance
        modelBuilder.Entity<WorkflowStepRecord>()
            .HasOne(wsr => wsr.WorkflowInstance)
            .WithMany(wi => wi.StepRecords)
            .HasForeignKey(wsr => wsr.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowStepRecord → WorkflowState
        modelBuilder.Entity<WorkflowStepRecord>()
            .HasOne(wsr => wsr.WorkflowState)
            .WithMany()
            .HasForeignKey(wsr => wsr.WorkflowStateId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowStepRecord → CompletedBy (ApplicationUser)
        modelBuilder.Entity<WorkflowStepRecord>()
            .HasOne(wsr => wsr.CompletedBy)
            .WithMany()
            .HasForeignKey(wsr => wsr.CompletedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Validation → FileMaster
        modelBuilder.Entity<Validation>()
            .HasOne(v => v.FileMaster)
            .WithMany()
            .HasForeignKey(v => v.FileMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Validation → Property
        modelBuilder.Entity<Validation>()
            .HasOne(v => v.Property)
            .WithMany()
            .HasForeignKey(v => v.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Validation → AssignedTo (ApplicationUser)
        modelBuilder.Entity<Validation>()
            .HasOne(v => v.AssignedTo)
            .WithMany()
            .HasForeignKey(v => v.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        // ApplicationUser → OrgUnit
        modelBuilder.Entity<ApplicationUser>()
            .HasOne(au => au.OrgUnit)
            .WithMany()
            .HasForeignKey(au => au.OrgUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrganisationalUnit → Province
        modelBuilder.Entity<OrganisationalUnit>()
            .HasOne(ou => ou.Province)
            .WithMany()
            .HasForeignKey(ou => ou.ProvinceId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrganisationalUnit → WMA
        modelBuilder.Entity<OrganisationalUnit>()
            .HasOne(ou => ou.WaterManagementArea)
            .WithMany()
            .HasForeignKey(ou => ou.WmaId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrganisationalUnit self-referencing (parent)
        modelBuilder.Entity<OrganisationalUnit>()
            .HasOne(ou => ou.ParentOrgUnit)
            .WithMany(ou => ou.ChildOrgUnits)
            .HasForeignKey(ou => ou.ParentOrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // WaterManagementArea → Province
        modelBuilder.Entity<WaterManagementArea>()
            .HasOne(wma => wma.Province)
            .WithMany(p => p.WaterManagementAreas)
            .HasForeignKey(wma => wma.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        // CatchmentArea → WMA
        modelBuilder.Entity<CatchmentArea>()
            .HasOne(c => c.WaterManagementArea)
            .WithMany()
            .HasForeignKey(c => c.WmaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CatchmentArea>()
            .HasIndex(c => c.CatchmentCode)
            .IsUnique();

        // Property → CatchmentArea
        modelBuilder.Entity<Property>()
            .HasOne(p => p.CatchmentArea)
            .WithMany(c => c.Properties)
            .HasForeignKey(p => p.CatchmentAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        // FileMaster → CatchmentArea
        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.CatchmentArea)
            .WithMany(c => c.FileMasters)
            .HasForeignKey(fm => fm.CatchmentAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrganisationalUnit → CatchmentArea
        modelBuilder.Entity<OrganisationalUnit>()
            .HasOne(ou => ou.CatchmentArea)
            .WithMany()
            .HasForeignKey(ou => ou.CatchmentAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        // GwcaProclamationRule → GovernmentWaterControlArea
        modelBuilder.Entity<GwcaProclamationRule>()
            .HasOne(r => r.WaterControlArea)
            .WithMany(g => g.ProclamationRules)
            .HasForeignKey(r => r.WaterControlAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GwcaProclamationRule>()
            .HasIndex(r => new { r.WaterControlAreaId, r.RuleCode });

        // Mapbook → FileMaster
        modelBuilder.Entity<Mapbook>()
            .HasOne(m => m.FileMaster)
            .WithMany(f => f.Mapbooks)
            .HasForeignKey(m => m.FileMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mapbook → Document
        modelBuilder.Entity<Mapbook>()
            .HasOne(m => m.Document)
            .WithMany()
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mapbook → Period
        modelBuilder.Entity<Mapbook>()
            .HasOne(m => m.Period)
            .WithMany()
            .HasForeignKey(m => m.PeriodId)
            .OnDelete(DeleteBehavior.SetNull);

        // MapbookImage → Mapbook
        modelBuilder.Entity<MapbookImage>()
            .HasOne(mi => mi.Mapbook)
            .WithMany(m => m.MapbookImages)
            .HasForeignKey(mi => mi.MapbookId)
            .OnDelete(DeleteBehavior.Cascade);

        // MapbookImage → SateliteImage
        modelBuilder.Entity<MapbookImage>()
            .HasOne(mi => mi.SateliteImage)
            .WithMany()
            .HasForeignKey(mi => mi.SateliteImageId)
            .OnDelete(DeleteBehavior.Restrict);

        // LetterIssuance → IrrigationBoard (for S33(2) declarations)
        modelBuilder.Entity<LetterIssuance>()
            .HasOne(li => li.IrrigationBoard)
            .WithMany()
            .HasForeignKey(li => li.IrrigationBoardId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Global: disable cascade delete for all relationships ──
        // SQL Server does not allow multiple cascade paths; Restrict is safer
        // and forces explicit deletion in correct order.
        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
