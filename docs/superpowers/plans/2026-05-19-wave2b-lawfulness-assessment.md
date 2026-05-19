# Wave 2b: LawfulnessAssessmentService Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the two-tier ELU lawfulness determination engine (GWCA rules first, S9B general principles fallback), wired to a FileMaster "Assess ELU" action that satisfies the CP7 guard and renders a detailed breakdown on the Details page.

**Architecture:** Mirror the existing CalculatorEngine pattern — a pure static `LawfulnessCalculator` for all arithmetic (unit-testable, no DB), orchestrated by a DI `LawfulnessAssessmentService` that loads data, calls the calculator, upserts a `LawfulnessAssessmentResult`, and creates/updates the `Entitlement` that satisfies `Cp7EluGuard` (`FileMaster.EntitlementId.HasValue`). The two-tier rule lookup uses `GwcaProclamationRule` rows already seeded for Blyde River; the general path uses hardcoded S9B statute constants.

**Tech Stack:** ASP.NET Core 10, EF Core 10 (in-memory provider for tests), SQL Server 2022, xUnit

---

## Domain cheat-sheet (read before implementing)

### SAPWAT volume conversion
`SAPWATCalculationResult` on `FieldAndCrop` is a **rate** in mm/ha/a (after rotation factor applied).  
To convert to m³: `volume = CropArea × SAPWATCalculationResult × 10`  
(1 mm depth over 1 ha = 10 m³ — see `SapwatCalculator.ComputeVolume`.)

### GWCA rule codes (Blyde River seed data)
| RuleCode | Unit | Meaning |
|---|---|---|
| `MAX_HECTARES` | ha | Base allowance: whichever is greater — this limit OR 40% of irrigable area |
| `MAX_IRRIGABLE_PCT` | pct | Absolute cap as % of irrigable area (e.g. 53) |
| `MAX_VOLUME_PER_HA` | m³/ha | Max abstraction rate per allowed irrigated hectare |
| `MAX_STORAGE_PER_HA` | m³/ha | Max storage per allowed irrigated hectare |
| `MAX_STORAGE_PER_PROPERTY` | m³ | Absolute per-property storage cap |

### S9B statutory limits (general path — from statute, not DB)
- Storage without permit: 250,000 m³
- Abstraction rate without permit: 110 l/s = 3,468,960 m³/year (110 × 31,536,000 s ÷ 1,000)

### CP7 guard contract
`Cp7EluGuard` (in `Services/Workflow/Guards/FlagGuards.cs`) checks only:
```csharp
ctx.FileMaster.EntitlementId.HasValue
```
Setting `FileMaster.EntitlementId` to a new `Entitlement.EntitlementId` satisfies this guard.

---

## File structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Models/Property.cs` | Modify | Add `WaterControlAreaId` FK + nav + `IrrigableAreaHa` |
| `Models/LawfulnessAssessmentResult.cs` | **New** | 1:1 with FileMaster — stores full ELU breakdown |
| `DatabaseContexts/ApplicationDBContext.cs` | Modify | DbSet + HasKey + unique index + FKs; cascade whitelist entry |
| `Services/SeedDataService.cs` | Modify | Add `SeedEntitlementTypesAsync` called from `SeedAsync` |
| `Services/Calculator/LawfulnessCalculator.cs` | **New** | Pure static arithmetic: `LawfulnessInput`, `LawfulnessOutput`, `Compute`, `ComputeGeneral`, `ComputeGwca` |
| `Interfaces/ILawfulnessAssessmentService.cs` | **New** | Interface with `Task<LawfulnessAssessmentResult> AssessAsync(Guid fileMasterId)` |
| `Services/LawfulnessAssessmentService.cs` | **New** | DI service: loads data, calls calculator, persists result + Entitlement |
| `Program.cs` | Modify | Register `ILawfulnessAssessmentService` |
| `Controllers/FileMasterController.cs` | Modify | Inject `ILawfulnessAssessmentService`; add `AssessLawfulness` POST; load result in `Details` |
| `Controllers/PropertyController.cs` | Modify | Add GWCA dropdown to `Edit` GET (Register GET optional) |
| `ViewModels/FileMasterDetailsViewModel.cs` | Modify | Add `LawfulnessAssessmentResult?` |
| `Views/FileMaster/Details.cshtml` | Modify | ELU Assessment panel (result table + "Assess ELU" button) |
| `Views/Property/Edit.cshtml` | Modify | GWCA dropdown + IrrigableArea input |
| `Tests/Services/Calculator/LawfulnessCalculatorTests.cs` | **New** | Unit tests for static calculator |
| `Tests/Services/LawfulnessAssessmentServiceTests.cs` | **New** | Integration tests with in-memory DB |

---

## Task 1 — Data model, DBContext, migration, EntitlementType seed

**Files:**
- Modify: `Models/Property.cs`
- Create: `Models/LawfulnessAssessmentResult.cs`
- Modify: `DatabaseContexts/ApplicationDBContext.cs`
- Modify: `Services/SeedDataService.cs`

- [ ] **Step 1: Add two columns to `Models/Property.cs`**

Insert after the `SuccessorPropertyId` / `SuccessorProperty` block (around line 57):

```csharp
[Display(Name = "Government Water Control Area")]
public Guid? WaterControlAreaId { get; set; }
public GovernmentWaterControlArea? GovernmentWaterControlArea { get; set; }

[Display(Name = "Irrigable Area (ha)")]
[Column(TypeName = "decimal(18, 2)")]
public decimal? IrrigableAreaHa { get; set; }
```

- [ ] **Step 2: Create `Models/LawfulnessAssessmentResult.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LawfulnessAssessmentResult
{
    public Guid LawfulnessAssessmentResultId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }

    public required string LegalFramework { get; set; } // "General" | "GWCA"
    public Guid? GwcaId { get; set; }
    public GovernmentWaterControlArea? Gwca { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalIrrigatedAreaHa { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalIrrigationDemandM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulIrrigationM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulIrrigationM3 { get; set; }
    public string? IrrigationLimitApplied { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalDamCapacityM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulStorageM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulStorageM3 { get; set; }
    public string? StorageLimitApplied { get; set; }

    [Display(Name = "Assessed")]
    public DateTime AssessedAt { get; set; }
}
```

- [ ] **Step 3: Update `DatabaseContexts/ApplicationDBContext.cs`**

3a. Add to cascade whitelist array (after the PAJAChecklist entry, before the closing `};`):
```csharp
// ELU lawfulness result cascades with its parent case
(typeof(LawfulnessAssessmentResult), typeof(FileMaster), nameof(LawfulnessAssessmentResult.FileMasterId), DeleteBehavior.Cascade),
```

3b. Add DbSet after the `PAJAChecklists` DbSet:
```csharp
public DbSet<LawfulnessAssessmentResult> LawfulnessAssessmentResults { get; set; }
```

3c. Add to `OnModelCreating` after the PAJAChecklist block (around line 280):
```csharp
// LawfulnessAssessmentResult → FileMaster (1:1 — one ELU result per case)
modelBuilder.Entity<LawfulnessAssessmentResult>().HasKey(e => e.LawfulnessAssessmentResultId);
modelBuilder.Entity<LawfulnessAssessmentResult>()
    .HasIndex(e => e.FileMasterId).IsUnique();
modelBuilder.Entity<LawfulnessAssessmentResult>()
    .HasOne(e => e.FileMaster).WithOne()
    .HasForeignKey<LawfulnessAssessmentResult>(e => e.FileMasterId)
    .OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<LawfulnessAssessmentResult>()
    .HasOne(e => e.Gwca).WithMany()
    .HasForeignKey(e => e.GwcaId)
    .OnDelete(DeleteBehavior.SetNull);

// Property → GovernmentWaterControlArea (many properties may fall in one GWCA)
modelBuilder.Entity<Property>()
    .HasOne(p => p.GovernmentWaterControlArea).WithMany()
    .HasForeignKey(p => p.WaterControlAreaId)
    .OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 4: Add EntitlementType seed to `Services/SeedDataService.cs`**

4a. Add call to `SeedAsync` (after `SeedGwcaProclamationRulesAsync`):
```csharp
await SeedEntitlementTypesAsync();
```

4b. Add the method:
```csharp
// ── 8. Entitlement Types ─────────────────────────────────────────────
private async Task SeedEntitlementTypesAsync()
{
    if (await _context.EntitlementTypes.AnyAsync())
        return;

    _context.EntitlementTypes.AddRange(
        new EntitlementType
        {
            EntitlementTypeId = Guid.NewGuid(),
            EntitlementName = "ELU_Irrigation",
            EntitlementDescription = "Existing Lawful Use — Irrigation (abstraction from water resource)"
        },
        new EntitlementType
        {
            EntitlementTypeId = Guid.NewGuid(),
            EntitlementName = "ELU_Storage",
            EntitlementDescription = "Existing Lawful Use — Storage (dam capacity)"
        },
        new EntitlementType
        {
            EntitlementTypeId = Guid.NewGuid(),
            EntitlementName = "ELU_SFRA",
            EntitlementDescription = "Existing Lawful Use — Stream Flow Reduction Activity (forestation)"
        });
    await _context.SaveChangesAsync();
}
```

- [ ] **Step 5: Build to verify no compile errors**

```bash
dotnet build
```
Expected: 0 Error(s)

- [ ] **Step 6: Run migration**

```bash
dotnet ef migrations add Wave2bLawfulness
dotnet ef database update
```

- [ ] **Step 7: Commit**

```bash
git add Models/Property.cs Models/LawfulnessAssessmentResult.cs \
        DatabaseContexts/ApplicationDBContext.cs \
        Services/SeedDataService.cs \
        Migrations/
git commit -m "feat(wave2b): data model + migration for ELU lawfulness assessment"
```

---

## Task 2 — Pure static LawfulnessCalculator

**Files:**
- Create: `Services/Calculator/LawfulnessCalculator.cs`
- Create: `Tests/Services/Calculator/LawfulnessCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests first**

Create `Tests/Services/Calculator/LawfulnessCalculatorTests.cs`:

```csharp
using dwa_ver_val.Services.Calculator;
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class LawfulnessCalculatorTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static LawfulnessInput GeneralInput(
        decimal irrigatedHa = 10m,
        decimal demandM3 = 80_000m,
        decimal damCapacityM3 = 50_000m,
        decimal? irrigableHa = null)
        => new(irrigatedHa, demandM3, damCapacityM3, irrigableHa, false, Array.Empty<GwcaProclamationRule>());

    private static LawfulnessInput GwcaInput(
        decimal irrigatedHa = 20m,
        decimal demandM3 = 150_000m,
        decimal damCapacityM3 = 40_000m,
        decimal? irrigableHa = 80m,
        decimal maxHa = 30m,
        decimal maxIrrigablePct = 53m,
        decimal maxVolPerHa = 9_900m,
        decimal maxStoragePerHa = 5_000m,
        decimal maxStoragePerProp = 50_000m)
    {
        var rules = new List<GwcaProclamationRule>
        {
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_HECTARES",            IsActive = true, NumericLimit = maxHa,           Unit = "ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_IRRIGABLE_PCT",       IsActive = true, NumericLimit = maxIrrigablePct,  Unit = "pct" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_VOLUME_PER_HA",       IsActive = true, NumericLimit = maxVolPerHa,      Unit = "m3/ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_STORAGE_PER_HA",      IsActive = true, NumericLimit = maxStoragePerHa,  Unit = "m3/ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_STORAGE_PER_PROPERTY",IsActive = true, NumericLimit = maxStoragePerProp,Unit = "m3" },
        };
        return new(irrigatedHa, demandM3, damCapacityM3, irrigableHa, true, rules);
    }

    // ── General path ─────────────────────────────────────────────────────────

    [Fact]
    public void ComputeGeneral_BelowBothLimits_AllLawful()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(demandM3: 80_000m, damCapacityM3: 50_000m));
        Assert.Equal("General", result.LegalFramework);
        Assert.Equal(80_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(50_000m, result.LawfulStorageM3);
        Assert.Equal(0m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGeneral_DamExceedsS9bLimit_StorageCapped()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(damCapacityM3: 300_000m));
        Assert.Equal(250_000m, result.LawfulStorageM3);
        Assert.Equal(50_000m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGeneral_DemandExceedsS9bAbstraction_IrrigationCapped()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(demandM3: 4_000_000m));
        Assert.Equal(LawfulnessCalculator.S9bAbstractionLimitM3PerYear, result.LawfulIrrigationM3);
        Assert.True(result.UnlawfulIrrigationM3 > 0m);
    }

    [Fact]
    public void Compute_WhenNotInGwca_DelegatesToGeneral()
    {
        var input = GeneralInput(); // IsInGwca = false
        var result = LawfulnessCalculator.Compute(input);
        Assert.Equal("General", result.LegalFramework);
    }

    // ── GWCA path ─────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeGwca_WithinAllLimits_AllLawful()
    {
        // irrigated=20ha, irrigable=80ha
        // maxHa = max(30, 0.40×80) = max(30, 32) = 32 ha
        // pctCap = 0.53×80 = 42.4 ha
        // allowedHa = min(20, min(32, 42.4)) = min(20, 32) = 20 ha
        // irrigationLimit = 9900 × 20 = 198,000 m³
        // demand=150,000 < 198,000 → all lawful
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 20m, demandM3: 150_000m, damCapacityM3: 40_000m, irrigableHa: 80m));
        Assert.Equal("GWCA", result.LegalFramework);
        Assert.Equal(150_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(40_000m, result.LawfulStorageM3);  // 5000×20=100,000 > 40,000 and < 50,000 prop limit
        Assert.Equal(0m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGwca_DemandExceedsVolumePerHaLimit_IrrigationCapped()
    {
        // allowedHa = 20 (all within limits), irrigationLimit = 9900×20 = 198,000
        // demand=250,000 > 198,000 → unlawful = 52,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 20m, demandM3: 250_000m, irrigableHa: 80m));
        Assert.Equal(198_000m, result.LawfulIrrigationM3);
        Assert.Equal(52_000m, result.UnlawfulIrrigationM3);
    }

    [Fact]
    public void ComputeGwca_DamExceedsPerPropertyLimit_StorageCapped()
    {
        // dam=60,000 > MAX_STORAGE_PER_PROPERTY=50,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(damCapacityM3: 60_000m));
        Assert.Equal(50_000m, result.LawfulStorageM3);
        Assert.Equal(10_000m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGwca_IrrigatedAreaCappedByMaxHa_ReducesVolumeLimit()
    {
        // irrigated=50ha, irrigable=60ha
        // maxHa = max(30, 0.40×60) = max(30, 24) = 30 ha
        // pctCap = 0.53×60 = 31.8 ha
        // allowedHa = min(50, min(30, 31.8)) = 30 ha
        // irrigationLimit = 9900 × 30 = 297,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 50m, demandM3: 300_000m, irrigableHa: 60m));
        Assert.Equal(297_000m, result.LawfulIrrigationM3);
        Assert.Equal(3_000m, result.UnlawfulIrrigationM3);
    }

    [Fact]
    public void Compute_WhenInGwca_DelegatesToGwca()
    {
        var input = GwcaInput();
        var result = LawfulnessCalculator.Compute(input);
        Assert.Equal("GWCA", result.LegalFramework);
    }
}
```

- [ ] **Step 2: Run tests — confirm they fail**

```bash
dotnet test --filter "LawfulnessCalculatorTests" 2>&1 | tail -5
```
Expected: build error — `LawfulnessCalculator` does not exist yet.

- [ ] **Step 3: Implement `Services/Calculator/LawfulnessCalculator.cs`**

```csharp
namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Input to the two-tier ELU lawfulness calculation.
/// Built by <see cref="LawfulnessAssessmentService"/> from DB data.
/// </summary>
public record LawfulnessInput(
    decimal TotalIrrigatedAreaHa,
    decimal TotalIrrigationDemandM3,
    decimal TotalDamCapacityM3,
    decimal? PropertyIrrigableAreaHa,
    bool IsInGwca,
    IReadOnlyList<GwcaProclamationRule> GwcaRules);

/// <summary>
/// Output of the two-tier ELU lawfulness calculation.
/// </summary>
public record LawfulnessOutput(
    string LegalFramework,
    decimal LawfulIrrigationM3,
    decimal UnlawfulIrrigationM3,
    string IrrigationLimitApplied,
    decimal LawfulStorageM3,
    decimal UnlawfulStorageM3,
    string StorageLimitApplied);

/// <summary>
/// Pure two-tier ELU lawfulness calculator.
/// No DI, no DB — operates on data supplied by the caller.
/// Tier 1: GWCA proclamation rules (property-specific, from GwcaProclamationRule).
/// Tier 2: General S9B statutory limits (hardcoded from National Water Act).
/// </summary>
public static class LawfulnessCalculator
{
    // S9B statutory limits — hardcoded from statute (not configurable)
    public const decimal S9bStorageLimitM3 = 250_000m;
    public const decimal S9bAbstractionLimitM3PerYear = 3_468_960m; // 110 l/s × 31,536,000 s ÷ 1,000

    public static LawfulnessOutput Compute(LawfulnessInput input) =>
        input.IsInGwca ? ComputeGwca(input) : ComputeGeneral(input);

    public static LawfulnessOutput ComputeGeneral(LawfulnessInput input)
    {
        var lawfulIrrigation = Math.Min(input.TotalIrrigationDemandM3, S9bAbstractionLimitM3PerYear);
        var unlawfulIrrigation = input.TotalIrrigationDemandM3 - lawfulIrrigation;

        var lawfulStorage = Math.Min(input.TotalDamCapacityM3, S9bStorageLimitM3);
        var unlawfulStorage = input.TotalDamCapacityM3 - lawfulStorage;

        return new LawfulnessOutput(
            LegalFramework: "General",
            LawfulIrrigationM3: lawfulIrrigation,
            UnlawfulIrrigationM3: unlawfulIrrigation,
            IrrigationLimitApplied: $"S9B statutory limit: {S9bAbstractionLimitM3PerYear:N0} m³/year (110 l/s)",
            LawfulStorageM3: lawfulStorage,
            UnlawfulStorageM3: unlawfulStorage,
            StorageLimitApplied: $"S9B statutory limit: {S9bStorageLimitM3:N0} m³ without permit");
    }

    public static LawfulnessOutput ComputeGwca(LawfulnessInput input)
    {
        var rules = input.GwcaRules;
        var irrigableArea = input.PropertyIrrigableAreaHa ?? input.TotalIrrigatedAreaHa;

        // MAX_HECTARES: base entitlement — whichever is greater: rule limit OR 40% of irrigable
        var maxHaLimit = GetLimit(rules, "MAX_HECTARES") ?? 30m;
        var effectiveMaxHa = Math.Max(maxHaLimit, 0.40m * irrigableArea);

        // MAX_IRRIGABLE_PCT: absolute cap as percentage of irrigable area
        var maxPct = GetLimit(rules, "MAX_IRRIGABLE_PCT") ?? 53m;
        var pctCapHa = (maxPct / 100m) * irrigableArea;

        var allowedIrrigatedHa = Math.Min(input.TotalIrrigatedAreaHa,
                                          Math.Min(effectiveMaxHa, pctCapHa));

        // MAX_VOLUME_PER_HA: abstraction limit
        var maxVolPerHa = GetLimit(rules, "MAX_VOLUME_PER_HA") ?? 9_900m;
        var irrigationLimit = maxVolPerHa * allowedIrrigatedHa;
        var lawfulIrrigation = Math.Min(input.TotalIrrigationDemandM3, irrigationLimit);
        var unlawfulIrrigation = input.TotalIrrigationDemandM3 - lawfulIrrigation;

        // Storage: lesser of per-ha and per-property limits
        var maxStoragePerHa = GetLimit(rules, "MAX_STORAGE_PER_HA") ?? 5_000m;
        var maxStoragePerProp = GetLimit(rules, "MAX_STORAGE_PER_PROPERTY") ?? 50_000m;
        var storageLimit = Math.Min(maxStoragePerHa * allowedIrrigatedHa, maxStoragePerProp);
        var lawfulStorage = Math.Min(input.TotalDamCapacityM3, storageLimit);
        var unlawfulStorage = input.TotalDamCapacityM3 - lawfulStorage;

        return new LawfulnessOutput(
            LegalFramework: "GWCA",
            LawfulIrrigationM3: lawfulIrrigation,
            UnlawfulIrrigationM3: unlawfulIrrigation,
            IrrigationLimitApplied: $"GWCA MAX_VOLUME_PER_HA: {maxVolPerHa:N0} m³/ha × {allowedIrrigatedHa:N1} ha = {irrigationLimit:N0} m³",
            LawfulStorageM3: lawfulStorage,
            UnlawfulStorageM3: unlawfulStorage,
            StorageLimitApplied: $"GWCA: min({maxStoragePerHa:N0} m³/ha × {allowedIrrigatedHa:N1} ha, {maxStoragePerProp:N0} m³ per property) = {storageLimit:N0} m³");
    }

    private static decimal? GetLimit(IReadOnlyList<GwcaProclamationRule> rules, string code)
        => rules.FirstOrDefault(r => r.RuleCode == code && r.IsActive)?.NumericLimit;
}
```

- [ ] **Step 4: Run tests — confirm they pass**

```bash
dotnet test --filter "LawfulnessCalculatorTests" 2>&1 | tail -5
```
Expected: Passed! — Failed: 0, Passed: 9

- [ ] **Step 5: Confirm full suite still green**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: Passed! — Failed: 0, Passed: 238 (229 + 9 new)

- [ ] **Step 6: Commit**

```bash
git add Services/Calculator/LawfulnessCalculator.cs Tests/Services/Calculator/LawfulnessCalculatorTests.cs
git commit -m "feat(wave2b): pure LawfulnessCalculator — GWCA + general S9B paths, 9 tests"
```

---

## Task 3 — ILawfulnessAssessmentService + LawfulnessAssessmentService

**Files:**
- Create: `Interfaces/ILawfulnessAssessmentService.cs`
- Create: `Services/LawfulnessAssessmentService.cs`
- Create: `Tests/Services/LawfulnessAssessmentServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Tests/Services/LawfulnessAssessmentServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Calculator;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services;

public class LawfulnessAssessmentServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (ApplicationDBContext db, Guid fileMasterId, Guid propertyId) SeedBasicCase(
        Guid? gwcaId = null)
    {
        var db = TestDbContextFactory.Create();

        var eluType = new EntitlementType
        {
            EntitlementTypeId = Guid.NewGuid(),
            EntitlementName = "ELU_Irrigation",
            EntitlementDescription = "ELU Irrigation"
        };
        db.EntitlementTypes.Add(eluType);

        GovernmentWaterControlArea? gwca = null;
        if (gwcaId.HasValue)
        {
            gwca = new GovernmentWaterControlArea
            {
                WaterControlAreaId = gwcaId.Value,
                GovernmentWaterControlAreaName = "Test GWCA"
            };
            db.GovernmentWaterControlAreas.Add(gwca);

            db.GwcaProclamationRules.AddRange(
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_HECTARES",             IsActive = true, NumericLimit = 30m,     Unit = "ha",    RuleDescription = "Max ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_IRRIGABLE_PCT",        IsActive = true, NumericLimit = 53m,     Unit = "pct",   RuleDescription = "Max pct" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_VOLUME_PER_HA",        IsActive = true, NumericLimit = 9_900m,  Unit = "m3/ha", RuleDescription = "Max vol/ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_STORAGE_PER_HA",       IsActive = true, NumericLimit = 5_000m,  Unit = "m3/ha", RuleDescription = "Max storage/ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_STORAGE_PER_PROPERTY", IsActive = true, NumericLimit = 50_000m, Unit = "m3",    RuleDescription = "Max storage prop" }
            );
        }

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "P-LAW-01",
            SGCode = "SG-LAW-01",
            PropertySize = 100m,
            IrrigableAreaHa = 80m,
            WaterControlAreaId = gwca?.WaterControlAreaId
        };
        db.Properties.Add(property);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "WARMS-LAW-01",
            SurveyorGeneralCode = "SG-LAW-01",
            PrimaryCatchment = "A21A",
            QuaternaryCatchment = "A21A",
            FarmName = "Test Farm",
            FarmNumber = 1,
            RegistrationDivision = "RD",
            FarmPortion = "0"
        };
        db.FileMasters.Add(fm);

        db.SaveChanges();
        return (db, fm.FileMasterId, property.PropertyId);
    }

    private static void SeedFieldAndCrop(ApplicationDBContext db, Guid propertyId, decimal sapwatRate, decimal cropArea)
    {
        var qualifyingPeriod = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying Period: 1 Oct 1996 - 30 Sep 1998" };
        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var ws = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        db.Periods.Add(qualifyingPeriod);
        db.Crops.Add(crop);
        db.WaterSources.Add(ws);
        db.SaveChanges();

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = db.Properties.Find(propertyId)!,
            PropertyId = propertyId,
            Period = qualifyingPeriod,
            PeriodId = qualifyingPeriod.PeriodId,
            FieldArea = cropArea,
            CropArea = cropArea,
            Crop = crop,
            WaterSource = ws,
            SAPWATCalculationResult = sapwatRate,
            RotationFactor = 1m
        });
        db.SaveChanges();
    }

    private static void SeedDamCalculation(ApplicationDBContext db, Guid propertyId, decimal capacity)
    {
        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        db.Rivers.Add(river);
        db.SaveChanges();

        db.DamCalculations.Add(new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            Property = db.Properties.Find(propertyId)!,
            PropertyId = propertyId,
            River = river,
            RiverId = river.RiverId,
            CalculationDate = new DateOnly(2026, 1, 1),
            SateliteSurveyDate = new DateOnly(1998, 1, 1),
            DamCapacity = capacity,
            DamCalculationStatus = DamCalculationStatus.IN_PROGRESS
        });
        db.SaveChanges();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_GeneralPath_CreatesResultAndSetsEntitlement()
    {
        var (db, fileMasterId, propertyId) = SeedBasicCase();
        // 10 ha × 500 mm/ha × 10 = 50,000 m³ demand; dam 30,000 m³ — both within general limits
        SeedFieldAndCrop(db, propertyId, sapwatRate: 500m, cropArea: 10m);
        SeedDamCalculation(db, propertyId, capacity: 30_000m);

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal("General", result.LegalFramework);
        Assert.Equal(50_000m, result.TotalIrrigationDemandM3);
        Assert.Equal(50_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(30_000m, result.LawfulStorageM3);
        Assert.Equal(0m, result.UnlawfulStorageM3);

        // FileMaster.EntitlementId must be set (satisfies Cp7EluGuard)
        var fm = await db.FileMasters.FindAsync(fileMasterId);
        Assert.True(fm!.EntitlementId.HasValue);
        var ent = await db.Entitlements.FindAsync(fm.EntitlementId!.Value);
        Assert.Equal(50_000m, ent!.Volume);
    }

    [Fact]
    public async Task AssessAsync_GwcaPath_LegalFrameworkIsGwca()
    {
        var gwcaId = Guid.NewGuid();
        var (db, fileMasterId, propertyId) = SeedBasicCase(gwcaId: gwcaId);
        SeedFieldAndCrop(db, propertyId, sapwatRate: 500m, cropArea: 10m);

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal("GWCA", result.LegalFramework);
        Assert.Equal(gwcaId, result.GwcaId);
    }

    [Fact]
    public async Task AssessAsync_NoFieldAndCropRecords_ZeroDemand()
    {
        var (db, fileMasterId, _) = SeedBasicCase();
        // No FieldAndCrop, no DamCalculation seeded

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal(0m, result.TotalIrrigationDemandM3);
        Assert.Equal(0m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.TotalDamCapacityM3);
    }

    [Fact]
    public async Task AssessAsync_RunTwice_UpdatesExistingResultInPlace()
    {
        var (db, fileMasterId, propertyId) = SeedBasicCase();
        SeedFieldAndCrop(db, propertyId, sapwatRate: 200m, cropArea: 5m);

        var sut = new LawfulnessAssessmentService(db);
        var first = await sut.AssessAsync(fileMasterId);
        var firstId = first.LawfulnessAssessmentResultId;

        var second = await sut.AssessAsync(fileMasterId);
        Assert.Equal(firstId, second.LawfulnessAssessmentResultId); // same row updated
    }

    [Fact]
    public async Task AssessAsync_MissingFileMaster_ThrowsInvalidOperation()
    {
        var db = TestDbContextFactory.Create();
        var sut = new LawfulnessAssessmentService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssessAsync(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run tests — confirm they fail (build error)**

```bash
dotnet test --filter "LawfulnessAssessmentServiceTests" 2>&1 | tail -5
```
Expected: build error — `LawfulnessAssessmentService` not found.

- [ ] **Step 3: Create `Interfaces/ILawfulnessAssessmentService.cs`**

```csharp
public interface ILawfulnessAssessmentService
{
    Task<LawfulnessAssessmentResult> AssessAsync(Guid fileMasterId);
}
```

- [ ] **Step 4: Create `Services/LawfulnessAssessmentService.cs`**

```csharp
using dwa_ver_val.Services.Calculator;
using Microsoft.EntityFrameworkCore;

public class LawfulnessAssessmentService : ILawfulnessAssessmentService
{
    private readonly ApplicationDBContext _db;

    public LawfulnessAssessmentService(ApplicationDBContext db) => _db = db;

    public async Task<LawfulnessAssessmentResult> AssessAsync(Guid fileMasterId)
    {
        // Load FileMaster + Property (including GWCA with active rules)
        var fm = await _db.FileMasters
            .Include(f => f.Property)
                .ThenInclude(p => p!.GovernmentWaterControlArea)
                    .ThenInclude(g => g!.ProclamationRules)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        var property = fm.Property
            ?? throw new InvalidOperationException("FileMaster has no linked Property.");

        // Qualifying period IDs (PeriodName contains "Qualifying")
        var qualifyingPeriodIds = await _db.Periods
            .Where(p => p.PeriodName.Contains("Qualifying"))
            .Select(p => p.PeriodId)
            .ToListAsync();

        // FieldAndCrop records for this property's qualifying period
        var fieldAndCrops = await _db.FieldAndCrops
            .Where(fc => fc.PropertyId == property.PropertyId
                      && qualifyingPeriodIds.Contains(fc.PeriodId))
            .ToListAsync();

        // DamCalculation records for this property
        var damCalcs = await _db.DamCalculations
            .Where(d => d.PropertyId == property.PropertyId)
            .ToListAsync();

        // Aggregate inputs
        var totalIrrigatedArea = fieldAndCrops.Sum(fc => fc.CropArea);
        var totalDemand = fieldAndCrops
            .Where(fc => fc.SAPWATCalculationResult > 0)
            .Sum(fc => SapwatCalculator.ComputeVolume(fc.CropArea, fc.SAPWATCalculationResult));
        var totalDamCapacity = damCalcs.Sum(dc => dc.DamCapacity);

        // Determine GWCA membership and load active rules
        var isInGwca = property.WaterControlAreaId.HasValue;
        var gwcaRules = isInGwca && property.GovernmentWaterControlArea?.ProclamationRules is { } ruleSet
            ? (IReadOnlyList<GwcaProclamationRule>)ruleSet.Where(r => r.IsActive).ToList()
            : Array.Empty<GwcaProclamationRule>();

        var input = new LawfulnessInput(
            TotalIrrigatedAreaHa: totalIrrigatedArea,
            TotalIrrigationDemandM3: totalDemand,
            TotalDamCapacityM3: totalDamCapacity,
            PropertyIrrigableAreaHa: property.IrrigableAreaHa,
            IsInGwca: isInGwca,
            GwcaRules: gwcaRules);

        var output = LawfulnessCalculator.Compute(input);

        // Upsert LawfulnessAssessmentResult
        var existing = await _db.LawfulnessAssessmentResults
            .FirstOrDefaultAsync(r => r.FileMasterId == fileMasterId);

        if (existing is null)
        {
            existing = new LawfulnessAssessmentResult
            {
                LawfulnessAssessmentResultId = Guid.NewGuid(),
                FileMasterId = fileMasterId,
                LegalFramework = output.LegalFramework
            };
            _db.LawfulnessAssessmentResults.Add(existing);
        }

        existing.LegalFramework = output.LegalFramework;
        existing.GwcaId = property.WaterControlAreaId;
        existing.TotalIrrigatedAreaHa = totalIrrigatedArea;
        existing.TotalIrrigationDemandM3 = totalDemand;
        existing.LawfulIrrigationM3 = output.LawfulIrrigationM3;
        existing.UnlawfulIrrigationM3 = output.UnlawfulIrrigationM3;
        existing.IrrigationLimitApplied = output.IrrigationLimitApplied;
        existing.TotalDamCapacityM3 = totalDamCapacity;
        existing.LawfulStorageM3 = output.LawfulStorageM3;
        existing.UnlawfulStorageM3 = output.UnlawfulStorageM3;
        existing.StorageLimitApplied = output.StorageLimitApplied;
        existing.AssessedAt = DateTime.UtcNow;

        // Create or update Entitlement (lawful irrigation volume = the ELU outcome)
        var eluType = await _db.EntitlementTypes
            .FirstOrDefaultAsync(t => t.EntitlementName == "ELU_Irrigation")
            ?? throw new InvalidOperationException(
                "EntitlementType 'ELU_Irrigation' not found. Run seed data (SeedEntitlementTypesAsync) first.");

        if (fm.EntitlementId.HasValue)
        {
            var entitlement = await _db.Entitlements.FindAsync(fm.EntitlementId.Value);
            if (entitlement is not null)
                entitlement.Volume = output.LawfulIrrigationM3;
        }
        else
        {
            var newEntitlement = new Entitlement
            {
                EntitlementId = Guid.NewGuid(),
                Name = $"ELU — {fm.RegistrationNumber}",
                Volume = output.LawfulIrrigationM3,
                EntitlementTypeId = eluType.EntitlementTypeId
            };
            _db.Entitlements.Add(newEntitlement);
            fm.EntitlementId = newEntitlement.EntitlementId;
        }

        await _db.SaveChangesAsync();
        return existing;
    }
}
```

- [ ] **Step 5: Run tests — confirm they pass**

```bash
dotnet test --filter "LawfulnessAssessmentServiceTests" 2>&1 | tail -5
```
Expected: Passed! — Failed: 0, Passed: 5

- [ ] **Step 6: Confirm full suite green**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: Passed! — Failed: 0, Passed: 243 (238 + 5 new)

- [ ] **Step 7: Commit**

```bash
git add Interfaces/ILawfulnessAssessmentService.cs \
        Services/LawfulnessAssessmentService.cs \
        Tests/Services/LawfulnessAssessmentServiceTests.cs
git commit -m "feat(wave2b): LawfulnessAssessmentService — orchestrates DB load, calculator, Entitlement upsert"
```

---

## Task 4 — Program.cs registration + FileMasterController wiring

**Files:**
- Modify: `Program.cs`
- Modify: `Controllers/FileMasterController.cs`
- Modify: `ViewModels/FileMasterDetailsViewModel.cs`

- [ ] **Step 1: Register service in `Program.cs`**

Find the block where `ICalculatorService` is registered (search for `CalculatorService`). Add after it:

```csharp
builder.Services.AddScoped<ILawfulnessAssessmentService, LawfulnessAssessmentService>();
```

- [ ] **Step 2: Inject `ILawfulnessAssessmentService` into `FileMasterController`**

2a. Add field (after `private readonly ILetterService _letters;`):
```csharp
private readonly ILawfulnessAssessmentService _assessment;
```

2b. Update constructor signature and body (add `ILawfulnessAssessmentService assessment` param):
```csharp
public FileMasterController(
    IFileMaster fileMasterRepository,
    ApplicationDBContext context,
    IWorkflowService workflow,
    IScopedCaseQuery scope,
    ILetterService letters,
    ILawfulnessAssessmentService assessment)
{
    _fileMasterRepository = fileMasterRepository;
    _context = context;
    _workflow = workflow;
    _scope = scope;
    _letters = letters;
    _assessment = assessment;
}
```

- [ ] **Step 3: Add `AssessLawfulness` POST action to `FileMasterController`**

Place immediately after the `RecordCpEvidence` POST action. Do not place it inside any other method.

```csharp
// POST: FileMaster/AssessLawfulness/{id}
// Triggers the two-tier ELU lawfulness assessment (CP7), creates/updates the
// LawfulnessAssessmentResult and the linked Entitlement, satisfying Cp7EluGuard.
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = DwsPolicies.CanCapture)]
public async Task<IActionResult> AssessLawfulness(Guid id)
{
    var fm = await _fileMasterRepository.GetByIdAsync(id);
    if (fm is null) return NotFound();
    if (!_scope.IsInScope(fm, User)) return Forbid();

    try
    {
        var result = await _assessment.AssessAsync(id);
        TempData["Success"] =
            $"ELU assessment complete ({result.LegalFramework} framework). " +
            $"Lawful irrigation: {result.LawfulIrrigationM3:N0} m³  |  " +
            $"Lawful storage: {result.LawfulStorageM3:N0} m³";
    }
    catch (InvalidOperationException ex)
    {
        TempData["Error"] = ex.Message;
    }
    return RedirectToAction(nameof(Details), new { id });
}
```

- [ ] **Step 4: Load `LawfulnessAssessmentResult` in the `Details` GET action**

Inside the `Details` action, after the `vm.PAJAChecklist = ...` line, add:
```csharp
vm.LawfulnessAssessmentResult = await _context.LawfulnessAssessmentResults
    .Include(r => r.Gwca)
    .FirstOrDefaultAsync(r => r.FileMasterId == fileMaster.FileMasterId);
```

- [ ] **Step 5: Add `LawfulnessAssessmentResult?` to `ViewModels/FileMasterDetailsViewModel.cs`**

Add after `PAJAChecklist?`:
```csharp
public LawfulnessAssessmentResult? LawfulnessAssessmentResult { get; set; }
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build
```
Expected: 0 Error(s)

- [ ] **Step 7: Commit**

```bash
git add Program.cs Controllers/FileMasterController.cs ViewModels/FileMasterDetailsViewModel.cs
git commit -m "feat(wave2b): wire AssessLawfulness action + Details result load into FileMasterController"
```

---

## Task 5 — UI: Property Edit (GWCA dropdown) + FileMaster Details (ELU panel)

**Files:**
- Modify: `Controllers/PropertyController.cs`
- Modify: `Views/Property/Edit.cshtml`
- Modify: `Views/FileMaster/Details.cshtml`

- [ ] **Step 1: Add GWCA dropdown to `PropertyController` Edit GET**

In `Controllers/PropertyController.cs`, find the `Edit` GET action (around line 127).
After the `CatchmentAreas` SelectList assignment, add:

```csharp
ViewBag.GovernmentWaterControlAreas = new SelectList(
    await _context.GovernmentWaterControlAreas
        .OrderBy(g => g.GovernmentWaterControlAreaName)
        .ToListAsync(),
    "WaterControlAreaId", "GovernmentWaterControlAreaName", property.WaterControlAreaId);
```

Also add the same line to the `Edit` POST action (in the ModelState.IsValid false branch, before `return View(property)`):
```csharp
ViewBag.GovernmentWaterControlAreas = new SelectList(
    await _context.GovernmentWaterControlAreas
        .OrderBy(g => g.GovernmentWaterControlAreaName)
        .ToListAsync(),
    "WaterControlAreaId", "GovernmentWaterControlAreaName", property.WaterControlAreaId);
```

- [ ] **Step 2: Add GWCA and IrrigableArea fields to `Views/Property/Edit.cshtml`**

After the `PropertySize` field block (the `<div class="col-md-3">` containing PropertySize), add:

```cshtml
<div class="col-md-3">
    <label asp-for="IrrigableAreaHa" class="form-label"></label>
    <input asp-for="IrrigableAreaHa" class="form-control" style="width:100%;" />
    <span asp-validation-for="IrrigableAreaHa" class="text-danger text-sm"></span>
</div>
<div class="col-md-6">
    <label asp-for="WaterControlAreaId" class="form-label">Government Water Control Area</label>
    <select asp-for="WaterControlAreaId" asp-items="ViewBag.GovernmentWaterControlAreas" class="form-control" style="width:100%;">
        <option value="">-- None (General S9B principles apply) --</option>
    </select>
    <span asp-validation-for="WaterControlAreaId" class="text-danger text-sm"></span>
</div>
```

- [ ] **Step 3: Add ELU Assessment panel to `Views/FileMaster/Details.cshtml`**

Find the closing `</div>` of the last content section (before or after the `_WorkflowPanel` partial inclusion). Add the following panel as a new section:

```cshtml
@* ── ELU Lawfulness Assessment (CP7) ────────────────────────────────── *@
<div style="margin-top: 24px;">
    <div style="display:flex; align-items:center; justify-content:space-between; margin-bottom:10px;">
        <h5 style="margin:0;">ELU Lawfulness Assessment</h5>
        <form asp-action="AssessLawfulness" asp-route-id="@Model.FileMaster.FileMasterId" method="post" style="display:inline;">
            @Html.AntiForgeryToken()
            <button type="submit" class="btn btn-sm btn-primary">
                @(Model.LawfulnessAssessmentResult is null ? "Run ELU Assessment" : "Re-Assess ELU")
            </button>
        </form>
    </div>

    @if (Model.LawfulnessAssessmentResult is null)
    {
        <div style="padding:12px 16px; background:#f8f9fa; border:1px solid #dee2e6; border-radius:6px; color:#6c757d; font-size:13px;">
            ELU assessment has not been run for this case yet. Run it to determine lawful and unlawful water use volumes (CP7).
        </div>
    }
    else
    {
        var r = Model.LawfulnessAssessmentResult;
        <div style="font-size:12px; color:#6c757d; margin-bottom:8px;">
            Legal framework: <strong>@r.LegalFramework</strong>
            @if (r.Gwca is not null) { <span> — @r.Gwca.GovernmentWaterControlAreaName</span> }
            &nbsp;|&nbsp; Assessed: @r.AssessedAt.ToString("dd MMM yyyy HH:mm") UTC
        </div>
        <table style="width:100%; font-size:13px; border-collapse:collapse;">
            <thead>
                <tr style="background:#f0f4f8; text-align:left;">
                    <th style="padding:6px 10px;">Category</th>
                    <th style="padding:6px 10px; text-align:right;">Total</th>
                    <th style="padding:6px 10px; text-align:right; color:#166534;">Lawful (m³)</th>
                    <th style="padding:6px 10px; text-align:right; color:#991b1b;">Unlawful (m³)</th>
                    <th style="padding:6px 10px;">Limit applied</th>
                </tr>
            </thead>
            <tbody>
                <tr style="border-top:1px solid #dee2e6;">
                    <td style="padding:6px 10px;">Irrigation</td>
                    <td style="padding:6px 10px; text-align:right;">@r.TotalIrrigationDemandM3.ToString("N0")</td>
                    <td style="padding:6px 10px; text-align:right; color:#166534; font-weight:600;">@r.LawfulIrrigationM3.ToString("N0")</td>
                    <td style="padding:6px 10px; text-align:right; color:@(r.UnlawfulIrrigationM3 > 0 ? "#991b1b" : "#6c757d"); font-weight:@(r.UnlawfulIrrigationM3 > 0 ? "600" : "400");">@r.UnlawfulIrrigationM3.ToString("N0")</td>
                    <td style="padding:6px 10px; font-size:11px; color:#6c757d;">@r.IrrigationLimitApplied</td>
                </tr>
                <tr style="border-top:1px solid #dee2e6;">
                    <td style="padding:6px 10px;">Storage (dams)</td>
                    <td style="padding:6px 10px; text-align:right;">@r.TotalDamCapacityM3.ToString("N0")</td>
                    <td style="padding:6px 10px; text-align:right; color:#166534; font-weight:600;">@r.LawfulStorageM3.ToString("N0")</td>
                    <td style="padding:6px 10px; text-align:right; color:@(r.UnlawfulStorageM3 > 0 ? "#991b1b" : "#6c757d"); font-weight:@(r.UnlawfulStorageM3 > 0 ? "600" : "400");">@r.UnlawfulStorageM3.ToString("N0")</td>
                    <td style="padding:6px 10px; font-size:11px; color:#6c757d;">@r.StorageLimitApplied</td>
                </tr>
            </tbody>
        </table>
        <div style="font-size:11px; color:#6c757d; margin-top:6px;">
            Irrigated area: @r.TotalIrrigatedAreaHa.ToString("N1") ha
        </div>
    }
</div>
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build
```
Expected: 0 Error(s)

- [ ] **Step 5: Run full test suite**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: Passed! — no regressions

- [ ] **Step 6: Commit**

```bash
git add Controllers/PropertyController.cs \
        Views/Property/Edit.cshtml \
        Views/FileMaster/Details.cshtml
git commit -m "feat(wave2b): Property GWCA/IrrigableArea edit fields + FileMaster ELU Assessment panel"
```

---

## Verification checklist

1. `dotnet build` — 0 errors
2. `dotnet test` — all tests pass (baseline 229 + 14 new = 243+)
3. `dotnet ef database update` — migration `Wave2bLawfulness` applies cleanly
4. Navigate to a Property Edit — GWCA dropdown and Irrigable Area field appear
5. Set a property to Blyde River GWCA and set IrrigableAreaHa
6. Ensure the property's FileMaster has FieldAndCrop records with SAPWATCalculationResult > 0
7. Navigate to the FileMaster Details — "Run ELU Assessment" button visible
8. Click "Run ELU Assessment" — success banner appears, ELU breakdown table renders
9. Re-click — "Re-Assess ELU" button works, same result row updated (no duplicate)
10. Verify `FileMaster.EntitlementId` is now set — the CP7 "Advance" button in the workflow panel should unblock
11. Test a case without GWCA — legal framework shows "General", limits are S9B constants
12. Test a GWCA case where demand exceeds limits — unlawful volume shown in red

---

## Files created / modified summary

| File | Action |
|------|--------|
| `Models/Property.cs` | WaterControlAreaId FK + nav + IrrigableAreaHa |
| `Models/LawfulnessAssessmentResult.cs` | **New** |
| `DatabaseContexts/ApplicationDBContext.cs` | DbSet + HasKey + unique index + 2 FK configs + cascade whitelist |
| `Services/SeedDataService.cs` | SeedEntitlementTypesAsync (3 types: ELU_Irrigation, ELU_Storage, ELU_SFRA) |
| `Services/Calculator/LawfulnessCalculator.cs` | **New** — LawfulnessInput, LawfulnessOutput, Compute/ComputeGeneral/ComputeGwca |
| `Interfaces/ILawfulnessAssessmentService.cs` | **New** |
| `Services/LawfulnessAssessmentService.cs` | **New** |
| `Program.cs` | AddScoped for ILawfulnessAssessmentService |
| `Controllers/FileMasterController.cs` | Inject _assessment; AssessLawfulness POST; load result in Details |
| `Controllers/PropertyController.cs` | GWCA SelectList in Edit GET + POST (error branch) |
| `ViewModels/FileMasterDetailsViewModel.cs` | LawfulnessAssessmentResult? property |
| `Views/FileMaster/Details.cshtml` | ELU Assessment panel |
| `Views/Property/Edit.cshtml` | IrrigableAreaHa input + WaterControlAreaId dropdown |
| `Tests/Services/Calculator/LawfulnessCalculatorTests.cs` | **New** — 9 unit tests |
| `Tests/Services/LawfulnessAssessmentServiceTests.cs` | **New** — 5 integration tests |
