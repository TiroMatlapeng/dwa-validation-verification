using Microsoft.EntityFrameworkCore;

public class ApplicationDBContext : DbContext
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> dbContextOption) : base(dbContextOption)
    {}

    public DbSet<Property> Properties { get; set; }
    public DbSet<Forestation> Forestations { get; set; }
    public DbSet<FieldAndCrop> FieldAndCrops { get; set; }
    public DbSet<Storing> Storings { get; set; }
    public DbSet<Crop> Crops { get; set; }
    public DbSet<DamCalculation> DamCalculations { get; set; }
    public DbSet<Irrigation> Irrigations { get; set; }
    public DbSet<Period> Periods { get; set; }
    public DbSet<WaterSource> WaterSources { get; set; }
    public DbSet<IrrigationSystem> IrrigationSystems { get; set; }
    public DbSet<CropType> CropTypes { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<AuthorisationType> AuthorisationTypes { get; set; }
    public DbSet<CustomerType> CustomerTypes { get; set; }
    public DbSet<Entitlement> Entitlements { get; set; }
    public DbSet<EntitlementType> EntitlementTypes { get; set; }
    public DbSet<FileMaster> FileMasters { get; set; }
    public DbSet<GovernmentWaterControlArea> GovernmentWaterControlAreas { get; set; }
    public DbSet<GovernmentWaterScheme> GovernmentWaterSchemes { get; set; }
    public DbSet<IrrigationBoard> IrrigationBoards { get; set; }
    public DbSet<IssuedLetters> IssuedLetters { get; set; }
    public DbSet<LetterIssuance> LetterIssuances { get; set; }
    public DbSet<LetterType> LetterTypes { get; set; }
    public DbSet<PropertyAddress> PropertyAddresses { get; set; }
    public DbSet<PropertyOwner> PropertyOwners { get; set; }
    public DbSet<PropertyOwnership> PropertyOwnerships { get; set; }
    public DbSet<River> Rivers { get; set; }
    public DbSet<SateliteImage> SateliteImages { get; set; }
    public DbSet<Validation> Validations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>().HasKey(e => e.PropertyId);
        modelBuilder.Entity<Forestation>().HasKey(e => e.ForestationId);
        modelBuilder.Entity<FieldAndCrop>().HasKey(e => e.FieldAndCropId);
        modelBuilder.Entity<Storing>().HasKey(e => e.StoringId);
        modelBuilder.Entity<Crop>().HasKey(e => e.CropId);
        modelBuilder.Entity<DamCalculation>().HasKey(e => e.DamCalculationId);
        modelBuilder.Entity<Irrigation>().HasKey(e => e.IrrigationId);
        modelBuilder.Entity<Period>().HasKey(e => e.PeriodId);
        modelBuilder.Entity<WaterSource>().HasKey(e => e.WaterSourceId);
        modelBuilder.Entity<IrrigationSystem>().HasKey(e => e.IrrigationSystemId);
        modelBuilder.Entity<CropType>().HasKey(e => e.CropTypeId);
        modelBuilder.Entity<Address>().HasKey(e => e.AddressId);
        modelBuilder.Entity<AuthorisationType>().HasKey(e => e.AuthorisationTypeId);
        modelBuilder.Entity<Entitlement>().HasKey(e => e.EntitlementId);
        modelBuilder.Entity<EntitlementType>().HasKey(e => e.EntitlementTypeId);
        modelBuilder.Entity<GovernmentWaterControlArea>().HasKey(e => e.WaterControlAreaId);
        modelBuilder.Entity<GovernmentWaterScheme>().HasKey(e => e.WaterSchemeId);
        modelBuilder.Entity<IrrigationBoard>().HasKey(e => e.IrrigationBoardId);
        modelBuilder.Entity<River>().HasKey(e => e.RiverId);
        modelBuilder.Entity<SateliteImage>().HasKey(e => e.ImageId);
        modelBuilder.Entity<FileMaster>().HasKey(e => e.Id);
        modelBuilder.Entity<IssuedLetters>().HasKey(e => e.Id);
        modelBuilder.Entity<LetterIssuance>().HasKey(e => e.Id);
        modelBuilder.Entity<LetterType>().HasKey(e => e.Id);
        modelBuilder.Entity<PropertyAddress>().HasKey(e => e.Id);
        modelBuilder.Entity<PropertyOwner>().HasKey(e => e.OwnerId);
        modelBuilder.Entity<PropertyOwnership>().HasKey(e => e.Id);
        modelBuilder.Entity<Validation>().HasKey(e => e.Id);
        modelBuilder.Entity<CustomerType>().HasKey(e => e.Id);

        modelBuilder.Entity<FileMaster>()
            .HasOne(fm => fm.PropertyAddress)
            .WithOne()
            .HasForeignKey<FileMaster>(fm => fm.PropertyAddressId);

        modelBuilder.Entity<PropertyOwnership>()
            .HasOne(po => po.Property)
            .WithMany(p => p.PropertyOwnerships)
            .HasForeignKey(po => po.PropertyId);

        modelBuilder.Entity<PropertyOwnership>()
            .HasOne(po => po.PropertyOwner)
            .WithMany(po => po.PropertyOwnerships)
            .HasForeignKey(po => po.PropertyOwnerId);
    }
}