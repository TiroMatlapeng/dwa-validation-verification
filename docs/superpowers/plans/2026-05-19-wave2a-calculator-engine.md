# Wave 2a — CalculatorEngine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the three V&V calculation engines — SAPWAT crop-water estimation, Appendix D dam volume (Methods 1 & 2), and SFRA stream-flow reduction — so that FieldAndCrop, DamCalculation, and Forestation records can have their results computed in-system rather than manually entered.

**Architecture:** Pure calculator classes (no DI, no DB) perform the arithmetic and are independently unit-tested. A thin `CalculatorService` (DI, `ICalculatorService`) wraps each calculator: it loads the required entity from the DB, calls the calculator, and writes the result back. Controllers expose POST endpoints that delegate to `CalculatorService`. Two new seeded lookup models (`CropWaterRate`, `SfraSpeciesRate`) supply the per-crop and per-species reference rates.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10 / SQL Server 2022, xUnit, EF Core In-Memory (tests). No new NuGet packages required.

---

> **Scope note:** Wave 2 has two independent subsystems. This plan covers **Wave 2a — CalculatorEngine only**. The **LawfulnessAssessmentService** (two-tier GWCA/general-principles legal determination) is Wave 2b and requires a separate design spec — it depends on the outputs of this plan.

---

## File Map

| Action | Path | Responsibility |
|--------|------|---------------|
| **Create** | `Models/CropWaterRate.cs` | Seeded lookup: CropId × IrrigationSystemId → mm/ha/a rate |
| **Create** | `Models/SfraSpeciesRate.cs` | Seeded lookup: species name → m³/ha/a rate |
| **Modify** | `Models/DamCalculation.cs` | Add 8 input columns for Method 1/2 |
| **Modify** | `DatabaseContexts/ApplicationDBContext.cs` | DbSets + HasKey for 2 new models |
| **Create** | `Migrations/<timestamp>_Wave2aCalculator.cs` | EF migration (generated) |
| **Create** | `Services/Calculator/SapwatCalculator.cs` | Pure SAPWAT computation |
| **Create** | `Services/Calculator/DamVolumeCalculator.cs` | Pure Method 1 + Method 2 |
| **Create** | `Services/Calculator/SfraCalculator.cs` | Pure SFRA ELU determination |
| **Create** | `Services/Calculator/ICalculatorService.cs` | DI contract |
| **Create** | `Services/Calculator/CalculatorService.cs` | DB load + compute + save |
| **Modify** | `Services/SeedDataService.cs` | Seed CropWaterRate + SfraSpeciesRate reference data |
| **Modify** | `Program.cs` | Register `ICalculatorService → CalculatorService` |
| **Modify** | `Controllers/DamCalculationController.cs` | Add `Calculate` POST action |
| **Modify** | `Controllers/FieldAndCropController.cs` | Add `Calculate` POST action |
| **Modify** | `Controllers/ForestationController.cs` | Add `CalculateSfra` POST action |
| **Modify** | `Views/DamCalculation/Edit.cshtml` | Method selector + input fields + Calculate button |
| **Modify** | `Views/FieldAndCrop/Edit.cshtml` | SAPWAT rate display + Calculate button |
| **Modify** | `Views/Forestation/Edit.cshtml` | SFRA Calculate button |
| **Create** | `Tests/Services/Calculator/SapwatCalculatorTests.cs` | Unit tests |
| **Create** | `Tests/Services/Calculator/DamVolumeCalculatorTests.cs` | Unit tests |
| **Create** | `Tests/Services/Calculator/SfraCalculatorTests.cs` | Unit tests |
| **Create** | `Tests/Services/Calculator/CalculatorServiceTests.cs` | Integration tests (in-memory DB) |

---

## Task 1 — Data Model Additions + Migration

### Files
- **Create:** `Models/CropWaterRate.cs`
- **Create:** `Models/SfraSpeciesRate.cs`
- **Modify:** `Models/DamCalculation.cs` — add 8 input columns
- **Modify:** `DatabaseContexts/ApplicationDBContext.cs` — 2 new DbSets + HasKey + FKs

---

- [ ] **Step 1.1 — Create `Models/CropWaterRate.cs`**

```csharp
using System.ComponentModel.DataAnnotations.Schema;

public class CropWaterRate
{
    public Guid CropWaterRateId { get; set; }
    public Guid CropId { get; set; }
    public Crop? Crop { get; set; }

    // Nullable: null means "applies to all irrigation systems for this crop"
    public Guid? IrrigationSystemId { get; set; }
    public IrrigationSystem? IrrigationSystem { get; set; }

    // Base demand rate in mm/ha/a (SAPWAT 4.0 or DWS calibrated value)
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RatePerHaPerAnnum { get; set; }

    public string? Source { get; set; } // e.g. "SAPWAT 4.0", "DWS Standard"
}
```

- [ ] **Step 1.2 — Create `Models/SfraSpeciesRate.cs`**

```csharp
using System.ComponentModel.DataAnnotations.Schema;

public class SfraSpeciesRate
{
    public Guid SfraSpeciesRateId { get; set; }
    public required string SpeciesName { get; set; }      // Matches Forestation.Specie (case-insensitive)

    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RateM3PerHaPerAnnum { get; set; }

    public string? Notes { get; set; }
}
```

- [ ] **Step 1.3 — Add 8 input columns to `Models/DamCalculation.cs`**

Add after the existing `DamNumber` property (after line ~13):

```csharp
// Calculation method and inputs — populated by the Calculate action, stored alongside DamCapacity result
public string? CalculationMethod { get; set; }  // "Method1" | "Method2"

// Method 1 (Wall Length) inputs
[Column(TypeName = "decimal(18, 2)")]
public decimal? WallLength { get; set; }         // metres

[Column(TypeName = "decimal(18, 2)")]
public decimal? Fetch { get; set; }              // metres (horizontal distance from wall to waterline)

[Column(TypeName = "decimal(18, 2)")]
public decimal? RiverDistance { get; set; }      // R1: horizontal river distance used for slope

[Column(TypeName = "decimal(18, 2)")]
public decimal? ContourDifference { get; set; }  // C1: vertical contour difference

// Method 2 (Area) inputs
[Column(TypeName = "decimal(18, 2)")]
public decimal? DamArea { get; set; }            // ha

[Column(TypeName = "decimal(18, 2)")]
public decimal? DamDepth { get; set; }           // metres

// Shared
[Column(TypeName = "decimal(18, 2)")]
public decimal? ShapeFactor { get; set; }        // 0.33 = ravine, 0.40 = bends, 0.50 = circular
```

- [ ] **Step 1.4 — Update `DatabaseContexts/ApplicationDBContext.cs`**

Add two DbSets after the existing `DamCalculations` DbSet (around line 41):
```csharp
public DbSet<CropWaterRate> CropWaterRates { get; set; }
public DbSet<SfraSpeciesRate> SfraSpeciesRates { get; set; }
```

Add HasKey + FK configs in `OnModelCreating`, before the global cascade override loop:
```csharp
modelBuilder.Entity<CropWaterRate>().HasKey(e => e.CropWaterRateId);
modelBuilder.Entity<CropWaterRate>()
    .HasOne(e => e.Crop).WithMany()
    .HasForeignKey(e => e.CropId).OnDelete(DeleteBehavior.Restrict);
modelBuilder.Entity<CropWaterRate>()
    .HasOne(e => e.IrrigationSystem).WithMany()
    .HasForeignKey(e => e.IrrigationSystemId).OnDelete(DeleteBehavior.SetNull);
modelBuilder.Entity<CropWaterRate>()
    .HasIndex(e => new { e.CropId, e.IrrigationSystemId })
    .IsUnique();

modelBuilder.Entity<SfraSpeciesRate>().HasKey(e => e.SfraSpeciesRateId);
modelBuilder.Entity<SfraSpeciesRate>()
    .HasIndex(e => e.SpeciesName).IsUnique();
```

- [ ] **Step 1.5 — Generate and apply the migration**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet ef migrations add Wave2aCalculator
dotnet ef database update
```

Expected: migration created, `Build succeeded`, database updated.

- [ ] **Step 1.6 — Verify build is clean**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Task 2 — SapwatCalculator (Pure, No DI)

### Files
- **Create:** `Services/Calculator/SapwatCalculator.cs`
- **Create:** `Tests/Services/Calculator/SapwatCalculatorTests.cs`

---

- [ ] **Step 2.1 — Write the failing tests first**

Create `Tests/Services/Calculator/SapwatCalculatorTests.cs`:

```csharp
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class SapwatCalculatorTests
{
    [Fact]
    public void ComputeRate_MultipliesLookupRateByRotationFactor()
    {
        // 600 mm/ha/a base rate, 0.75 rotation factor → 450 mm/ha/a effective
        var result = SapwatCalculator.ComputeRate(lookupRatePerHaPerAnnum: 600m, rotationFactor: 0.75m);
        Assert.Equal(450m, result);
    }

    [Fact]
    public void ComputeRate_RotationFactorOne_ReturnsFull Rate()
    {
        var result = SapwatCalculator.ComputeRate(800m, 1.0m);
        Assert.Equal(800m, result);
    }

    [Fact]
    public void ComputeVolume_ConvertsMmPerHaToM3()
    {
        // 500 mm/ha/a × 2 ha × 10 conversion = 10,000 m³/year
        var result = SapwatCalculator.ComputeVolume(cropAreaHa: 2m, ratePerHaPerAnnum: 500m);
        Assert.Equal(10_000m, result);
    }

    [Fact]
    public void ComputeVolume_ZeroArea_ReturnsZero()
    {
        var result = SapwatCalculator.ComputeVolume(0m, 600m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ComputeRate_ZeroRotationFactor_ReturnsZero()
    {
        var result = SapwatCalculator.ComputeRate(600m, 0m);
        Assert.Equal(0m, result);
    }
}
```

- [ ] **Step 2.2 — Run tests to confirm they fail**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet test --filter "SapwatCalculatorTests" 2>&1 | tail -5
```

Expected: compilation error because `SapwatCalculator` doesn't exist yet.

- [ ] **Step 2.3 — Create `Services/Calculator/SapwatCalculator.cs`**

```csharp
namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Pure SAPWAT crop-water demand calculator. No DI, no DB access — all inputs are passed in.
/// Formula source: SAPWAT 4.0 / DWS V&V Implementation Guide.
/// </summary>
public static class SapwatCalculator
{
    /// <summary>
    /// Applies the rotation factor to the seeded crop water demand rate.
    /// </summary>
    /// <param name="lookupRatePerHaPerAnnum">Reference rate in mm/ha/a from CropWaterRate seed data.</param>
    /// <param name="rotationFactor">FieldAndCrop.RotationFactor (0–1 for partial season crops, 1 for full season).</param>
    /// <returns>Effective demand rate in mm/ha/a to store in FieldAndCrop.SAPWATCalculationResult.</returns>
    public static decimal ComputeRate(decimal lookupRatePerHaPerAnnum, decimal rotationFactor)
        => lookupRatePerHaPerAnnum * rotationFactor;

    /// <summary>
    /// Converts an area × rate combination into an annual volume.
    /// 1 mm depth over 1 ha = 10 m³.
    /// </summary>
    /// <param name="cropAreaHa">FieldAndCrop.CropArea in hectares.</param>
    /// <param name="ratePerHaPerAnnum">Effective demand in mm/ha/a (output of ComputeRate).</param>
    /// <returns>Annual water volume in m³.</returns>
    public static decimal ComputeVolume(decimal cropAreaHa, decimal ratePerHaPerAnnum)
        => cropAreaHa * ratePerHaPerAnnum * 10m;
}
```

- [ ] **Step 2.4 — Run tests — expect green**

```bash
dotnet test --filter "SapwatCalculatorTests" 2>&1 | tail -5
```

Expected: `5 passed, 0 failed`

- [ ] **Step 2.5 — Commit**

```bash
git add Models/CropWaterRate.cs Models/SfraSpeciesRate.cs Models/DamCalculation.cs \
    DatabaseContexts/ApplicationDBContext.cs \
    Migrations/ \
    Services/Calculator/SapwatCalculator.cs \
    Tests/Services/Calculator/SapwatCalculatorTests.cs
git commit -m "feat(calculator): SAPWAT calculator + data model additions (Wave 2a Task 1-2)"
```

---

## Task 3 — DamVolumeCalculator (Pure, No DI)

### Files
- **Create:** `Services/Calculator/DamVolumeCalculator.cs`
- **Create:** `Tests/Services/Calculator/DamVolumeCalculatorTests.cs`

---

- [ ] **Step 3.1 — Write the failing tests**

Create `Tests/Services/Calculator/DamVolumeCalculatorTests.cs`:

```csharp
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class DamVolumeCalculatorTests
{
    // Method 1: Slope = R1/C1, Depth = Fetch/Slope, Capacity = WallLength × Fetch × Depth × Factor / 2
    [Fact]
    public void Method1_ComputesCorrectCapacity()
    {
        // R1=100m, C1=10m → Slope=10; Fetch=50m → Depth=5m
        // Capacity = 200 × 50 × 5 × 0.4 / 2 = 10,000 m³
        var result = DamVolumeCalculator.ComputeMethod1(
            wallLength: 200m,
            fetch: 50m,
            riverDistance: 100m,
            contourDifference: 10m,
            shapeFactor: 0.40m);
        Assert.Equal(10_000m, result);
    }

    [Fact]
    public void Method1_RavineShapeFactor_UsesPointThreeThree()
    {
        // R1=100m, C1=10m → Slope=10; Fetch=50m → Depth=5m
        // Capacity = 200 × 50 × 5 × 0.33 / 2 = 8,250 m³
        var result = DamVolumeCalculator.ComputeMethod1(200m, 50m, 100m, 10m, 0.33m);
        Assert.Equal(8_250m, result);
    }

    [Fact]
    public void Method1_ThrowsWhenContourDifferenceIsZero()
    {
        Assert.Throws<ArgumentException>(() =>
            DamVolumeCalculator.ComputeMethod1(200m, 50m, 100m, 0m, 0.4m));
    }

    // Method 2: Capacity = Area_ha × Depth_m × Factor × 1000
    [Fact]
    public void Method2_ComputesCorrectCapacity()
    {
        // 2 ha × 3 m × 0.5 × 1000 = 3,000 m³
        var result = DamVolumeCalculator.ComputeMethod2(
            areaHa: 2m,
            depthM: 3m,
            shapeFactor: 0.5m);
        Assert.Equal(3_000m, result);
    }

    [Fact]
    public void Method2_ZeroDepth_ReturnsZero()
    {
        var result = DamVolumeCalculator.ComputeMethod2(2m, 0m, 0.4m);
        Assert.Equal(0m, result);
    }
}
```

- [ ] **Step 3.2 — Run tests to confirm failure**

```bash
dotnet test --filter "DamVolumeCalculatorTests" 2>&1 | tail -5
```

Expected: compilation error.

- [ ] **Step 3.3 — Create `Services/Calculator/DamVolumeCalculator.cs`**

```csharp
namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Pure Appendix D dam volume calculator. No DI, no DB access.
/// Formula source: DWS V&V Requirements Ed.3 Appendix D.
/// </summary>
public static class DamVolumeCalculator
{
    /// <summary>
    /// Method 1 — Wall Length method.
    /// Slope = RiverDistance / ContourDifference
    /// Depth = Fetch / Slope
    /// Capacity (m³) = WallLength × Fetch × Depth × ShapeFactor / 2
    /// </summary>
    public static decimal ComputeMethod1(
        decimal wallLength,
        decimal fetch,
        decimal riverDistance,
        decimal contourDifference,
        decimal shapeFactor)
    {
        if (contourDifference == 0)
            throw new ArgumentException("ContourDifference must be non-zero to compute slope.", nameof(contourDifference));

        var slope = riverDistance / contourDifference;
        var depth = fetch / slope;
        return wallLength * fetch * depth * shapeFactor / 2m;
    }

    /// <summary>
    /// Method 2 — Area method.
    /// Capacity (m³) = Area (ha) × Depth (m) × ShapeFactor × 1000
    /// </summary>
    public static decimal ComputeMethod2(decimal areaHa, decimal depthM, decimal shapeFactor)
        => areaHa * depthM * shapeFactor * 1000m;
}
```

- [ ] **Step 3.4 — Run tests — expect green**

```bash
dotnet test --filter "DamVolumeCalculatorTests" 2>&1 | tail -5
```

Expected: `5 passed, 0 failed`

- [ ] **Step 3.5 — Commit**

```bash
git add Services/Calculator/DamVolumeCalculator.cs Tests/Services/Calculator/DamVolumeCalculatorTests.cs
git commit -m "feat(calculator): Appendix D dam volume calculator — Method 1 + Method 2 (Wave 2a Task 3)"
```

---

## Task 4 — SfraCalculator (Pure, No DI)

### Files
- **Create:** `Services/Calculator/SfraCalculator.cs`
- **Create:** `Tests/Services/Calculator/SfraCalculatorTests.cs`

---

- [ ] **Step 4.1 — Write the failing tests**

Create `Tests/Services/Calculator/SfraCalculatorTests.cs`:

```csharp
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class SfraCalculatorTests
{
    [Fact]
    public void Compute_Pre1972Only_FullyLawful()
    {
        // 10 ha pre-1972 planted, 0 permit, 10 ha qualifying → all lawful
        var result = SfraCalculator.Compute(
            pre1972Ha: 10m,
            sfraPermitHa: 0m,
            qualifyingHa: 10m,
            speciesRateM3PerHaPerAnnum: 6500m);

        Assert.Equal(10m, result.EluHa);
        Assert.Equal(10m, result.LawfulHa);
        Assert.Equal(0m, result.UnlawfulHa);
        Assert.Equal(65_000m, result.EluVolume);     // 10 × 6500
        Assert.Equal(65_000m, result.LawfulVolume);
        Assert.Equal(0m, result.UnlawfulVolume);
    }

    [Fact]
    public void Compute_PermitOnly_CapToPermitExtent()
    {
        // 0 pre-1972, 8 ha permit, 12 ha qualifying → 8 lawful, 4 unlawful
        var result = SfraCalculator.Compute(0m, 8m, 12m, 6500m);

        Assert.Equal(8m, result.EluHa);
        Assert.Equal(8m, result.LawfulHa);
        Assert.Equal(4m, result.UnlawfulHa);
        Assert.Equal(52_000m, result.EluVolume);
        Assert.Equal(52_000m, result.LawfulVolume);
        Assert.Equal(26_000m, result.UnlawfulVolume); // 4 × 6500
    }

    [Fact]
    public void Compute_Pre1972AndPermitCombined_SumsToQualifyingCap()
    {
        // 5 ha pre-1972 + 8 ha permit = 13 ha → capped to 10 ha qualifying
        var result = SfraCalculator.Compute(5m, 8m, 10m, 6500m);

        Assert.Equal(10m, result.EluHa);             // capped at qualifying total
        Assert.Equal(10m, result.LawfulHa);
        Assert.Equal(0m, result.UnlawfulHa);
    }

    [Fact]
    public void Compute_ZeroQualifying_AllZero()
    {
        var result = SfraCalculator.Compute(5m, 3m, 0m, 6500m);
        Assert.Equal(0m, result.EluHa);
        Assert.Equal(0m, result.UnlawfulHa);
    }
}
```

- [ ] **Step 4.2 — Run tests to confirm failure**

```bash
dotnet test --filter "SfraCalculatorTests" 2>&1 | tail -5
```

Expected: compilation error.

- [ ] **Step 4.3 — Create `Services/Calculator/SfraCalculator.cs`**

```csharp
namespace dwa_ver_val.Services.Calculator;

public record SfraResult(
    decimal EluHa,
    decimal EluVolume,
    decimal LawfulHa,
    decimal LawfulVolume,
    decimal UnlawfulHa,
    decimal UnlawfulVolume);

/// <summary>
/// Pure SFRA (Stream Flow Reduction Activity) ELU calculator.
/// Rules: Pre-1972 planting = fully lawful ELU; Post-1984 SFRA permit = lawful to extent of permit.
/// Any qualifying-period area beyond the authorised extent is unlawful.
/// </summary>
public static class SfraCalculator
{
    public static SfraResult Compute(
        decimal pre1972Ha,
        decimal sfraPermitHa,
        decimal qualifyingHa,
        decimal speciesRateM3PerHaPerAnnum)
    {
        if (qualifyingHa <= 0) return new SfraResult(0, 0, 0, 0, 0, 0);

        var authorisedHa = Math.Min(pre1972Ha + sfraPermitHa, qualifyingHa);
        var unlawfulHa = qualifyingHa - authorisedHa;

        var eluVolume = authorisedHa * speciesRateM3PerHaPerAnnum;
        var unlawfulVolume = unlawfulHa * speciesRateM3PerHaPerAnnum;

        return new SfraResult(
            EluHa: authorisedHa,
            EluVolume: eluVolume,
            LawfulHa: authorisedHa,
            LawfulVolume: eluVolume,
            UnlawfulHa: unlawfulHa,
            UnlawfulVolume: unlawfulVolume);
    }
}
```

- [ ] **Step 4.4 — Run tests — expect green**

```bash
dotnet test --filter "SfraCalculatorTests" 2>&1 | tail -5
```

Expected: `4 passed, 0 failed`

- [ ] **Step 4.5 — Commit**

```bash
git add Services/Calculator/SfraCalculator.cs Tests/Services/Calculator/SfraCalculatorTests.cs
git commit -m "feat(calculator): SFRA ELU calculator (Wave 2a Task 4)"
```

---

## Task 5 — Seed CropWaterRate + SfraSpeciesRate Reference Data

### Files
- **Modify:** `Services/SeedDataService.cs` — add `SeedCalculatorReferenceDataAsync`

---

- [ ] **Step 5.1 — Read `Services/SeedDataService.cs`** to find where `SeedAsync` is called and the pattern used for other seed methods. Locate the existing `SeedAsync` entry point.

- [ ] **Step 5.2 — Add `SeedCalculatorReferenceDataAsync` to `SeedDataService.cs`**

Add this method to the class, then call it from `SeedAsync`:

```csharp
private async Task SeedCalculatorReferenceDataAsync()
{
    // SFRA species rates (m³/ha/a) — DWS standard values
    var sfraRates = new[]
    {
        new { Species = "Eucalyptus", Rate = 6500m },
        new { Species = "Pine",       Rate = 5500m },
        new { Species = "Wattle",     Rate = 6000m },
        new { Species = "Gum",        Rate = 6500m },
    };

    foreach (var s in sfraRates)
    {
        if (!await _context.SfraSpeciesRates.AnyAsync(r => r.SpeciesName == s.Species))
        {
            _context.SfraSpeciesRates.Add(new SfraSpeciesRate
            {
                SfraSpeciesRateId = Guid.NewGuid(),
                SpeciesName = s.Species,
                RateM3PerHaPerAnnum = s.Rate,
                Notes = "DWS standard rate",
            });
        }
    }

    // CropWaterRate: system-wide defaults (no irrigation-system distinction at Wave 2a)
    // Rates in mm/ha/a sourced from SAPWAT 4.0 South African averages.
    // These are seeded against CropType names — get the CropType IDs from the seeded Crops.
    // Only seed if CropWaterRates table is empty to avoid duplicating on restart.
    if (!await _context.CropWaterRates.AnyAsync())
    {
        var crops = await _context.Crops.Include(c => c.CropType).ToListAsync();
        var defaultRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Maize"]        = 550m,
            ["Wheat"]        = 450m,
            ["Sugarcane"]    = 1200m,
            ["Soybean"]      = 500m,
            ["Sunflower"]    = 480m,
            ["Groundnut"]    = 520m,
            ["Cotton"]       = 700m,
            ["Lucerne"]      = 1400m,
            ["Pasture"]      = 800m,
            ["Vegetables"]   = 600m,
            ["Citrus"]       = 900m,
            ["Grapes"]       = 700m,
            ["Stone fruit"]  = 750m,
            ["Other"]        = 600m,
        };

        foreach (var crop in crops)
        {
            var cropName = crop.CropType?.Name ?? crop.CropName ?? "";
            if (!defaultRates.TryGetValue(cropName, out var rate))
                rate = 600m; // fallback default

            _context.CropWaterRates.Add(new CropWaterRate
            {
                CropWaterRateId = Guid.NewGuid(),
                CropId = crop.CropId,
                IrrigationSystemId = null,   // applies to all irrigation systems
                RatePerHaPerAnnum = rate,
                Source = "SAPWAT 4.0 SA average",
            });
        }
    }

    await _context.SaveChangesAsync();
}
```

> **Note:** You must read `Models/Crop.cs` before this step to confirm the property names used (`CropId`, `CropName` or equivalent). Adjust the property names to match what exists in the model.

- [ ] **Step 5.3 — Call the new seed method from `SeedAsync`**

Find the line in `SeedAsync` where other seed methods are awaited and add:
```csharp
await SeedCalculatorReferenceDataAsync();
```

- [ ] **Step 5.4 — Build and verify**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5.5 — Commit**

```bash
git add Services/SeedDataService.cs
git commit -m "feat(calculator): seed CropWaterRate + SfraSpeciesRate reference data (Wave 2a Task 5)"
```

---

## Task 6 — CalculatorService (DI, DB Load + Compute + Save)

### Files
- **Create:** `Services/Calculator/ICalculatorService.cs`
- **Create:** `Services/Calculator/CalculatorService.cs`
- **Modify:** `Program.cs` — register the service
- **Create:** `Tests/Services/Calculator/CalculatorServiceTests.cs`

---

- [ ] **Step 6.1 — Create `Services/Calculator/ICalculatorService.cs`**

```csharp
namespace dwa_ver_val.Services.Calculator;

public interface ICalculatorService
{
    /// <summary>Compute SAPWAT rate for one FieldAndCrop record; saves to SAPWATCalculationResult.</summary>
    Task<decimal> ComputeSapwatAsync(Guid fieldAndCropId);

    /// <summary>Compute dam capacity for one DamCalculation record; saves to DamCapacity.</summary>
    Task<decimal> ComputeDamVolumeAsync(Guid damCalculationId);

    /// <summary>Compute SFRA ELU for one Forestation record; saves ELU/Lawful/Unlawful fields.</summary>
    Task<SfraResult> ComputeSfraAsync(Guid forestationId);
}
```

- [ ] **Step 6.2 — Write the failing integration tests**

Create `Tests/Services/Calculator/CalculatorServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Calculator;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class CalculatorServiceTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ComputeSapwat_SetsCalculationResult()
    {
        using var db = NewDb();

        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var waterSource = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 10 };
        db.Crops.Add(crop);
        db.WaterSources.Add(waterSource);
        db.Periods.Add(period);
        db.Properties.Add(property);

        var rate = new CropWaterRate
        {
            CropWaterRateId = Guid.NewGuid(),
            CropId = crop.CropId,
            IrrigationSystemId = null,
            RatePerHaPerAnnum = 550m,
            Source = "Test",
        };
        db.CropWaterRates.Add(rate);

        var fc = new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            Property = property,
            Crop = crop,
            WaterSource = waterSource,
            Period = period,
            FieldArea = 5m,
            CropArea = 4m,
            RotationFactor = 0.75m,
        };
        db.FieldAndCrops.Add(fc);
        await db.SaveChangesAsync();

        var svc = new CalculatorService(db);
        var result = await svc.ComputeSapwatAsync(fc.FieldAndCropId);

        // 550 × 0.75 = 412.5
        Assert.Equal(412.5m, result);
        var saved = await db.FieldAndCrops.FindAsync(fc.FieldAndCropId);
        Assert.Equal(412.5m, saved!.SAPWATCalculationResult);
    }

    [Fact]
    public async Task ComputeDamVolume_Method2_SetsCapacity()
    {
        using var db = NewDb();
        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 10 };
        db.Rivers.Add(river);
        db.Properties.Add(property);

        var dam = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            Property = property,
            River = river,
            RiverId = river.RiverId,
            CalculationDate = DateOnly.FromDateTime(DateTime.Today),
            SateliteSurveyDate = DateOnly.FromDateTime(DateTime.Today),
            DamCapacity = 0m,
            CalculationMethod = "Method2",
            DamArea = 2m,
            DamDepth = 3m,
            ShapeFactor = 0.5m,
        };
        db.DamCalculations.Add(dam);
        await db.SaveChangesAsync();

        var svc = new CalculatorService(db);
        var result = await svc.ComputeDamVolumeAsync(dam.DamCalculationId);

        // 2 × 3 × 0.5 × 1000 = 3000 m³
        Assert.Equal(3_000m, result);
        var saved = await db.DamCalculations.FindAsync(dam.DamCalculationId);
        Assert.Equal(3_000m, saved!.DamCapacity);
    }

    [Fact]
    public async Task ComputeSfra_UpdatesForestationFields()
    {
        using var db = NewDb();
        var property = new Property { PropertyId = Guid.NewGuid(), PropertySize = 20 };
        db.Properties.Add(property);

        db.SfraSpeciesRates.Add(new SfraSpeciesRate
        {
            SfraSpeciesRateId = Guid.NewGuid(),
            SpeciesName = "Pine",
            RateM3PerHaPerAnnum = 5500m,
        });

        var forestation = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            Property = property,
            Specie = "Pine",
            Pre1972Hectares = 0m,
            SFRAPermitHectares = 8m,
            QualifyPeriodSFRAHectares = 12m,
            RegisteredHectares = 10m,
            RegisteredVolume = 55_000m,
        };
        db.Forestations.Add(forestation);
        await db.SaveChangesAsync();

        var svc = new CalculatorService(db);
        var result = await svc.ComputeSfraAsync(forestation.ForestationId);

        Assert.Equal(8m, result.EluHa);
        Assert.Equal(44_000m, result.EluVolume);  // 8 × 5500
        Assert.Equal(4m, result.UnlawfulHa);

        var saved = await db.Forestations.FindAsync(forestation.ForestationId);
        Assert.Equal(8m, saved!.ELUHectares);
        Assert.Equal(44_000m, saved!.ELUVolume);
        Assert.Equal(4m, saved!.UnlawfulHectares);
    }
}
```

- [ ] **Step 6.3 — Run tests to confirm failure**

```bash
dotnet test --filter "CalculatorServiceTests" 2>&1 | tail -5
```

Expected: compilation error — `CalculatorService` does not exist yet.

- [ ] **Step 6.4 — Create `Services/Calculator/CalculatorService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Calculator;

public class CalculatorService : ICalculatorService
{
    private readonly ApplicationDBContext _db;
    public CalculatorService(ApplicationDBContext db) { _db = db; }

    public async Task<decimal> ComputeSapwatAsync(Guid fieldAndCropId)
    {
        var fc = await _db.FieldAndCrops
            .Include(f => f.Crop)
            .FirstOrDefaultAsync(f => f.FieldAndCropId == fieldAndCropId)
            ?? throw new InvalidOperationException($"FieldAndCrop {fieldAndCropId} not found.");

        // Look up rate: prefer crop + irrigation system match, fall back to crop-only (IrrigationSystemId null)
        var irrigationSystemId = fc.IrrigationSystem?.IrrigationSystemId;
        var rate = await _db.CropWaterRates
            .Where(r => r.CropId == fc.Crop.CropId &&
                        (r.IrrigationSystemId == irrigationSystemId || r.IrrigationSystemId == null))
            .OrderByDescending(r => r.IrrigationSystemId != null)  // prefer specific match
            .Select(r => r.RatePerHaPerAnnum)
            .FirstOrDefaultAsync();

        if (rate == 0)
            throw new InvalidOperationException($"No CropWaterRate found for crop '{fc.Crop.CropId}'. Seed reference data first.");

        var effectiveRate = SapwatCalculator.ComputeRate(rate, fc.RotationFactor);
        fc.SAPWATCalculationResult = effectiveRate;
        await _db.SaveChangesAsync();
        return effectiveRate;
    }

    public async Task<decimal> ComputeDamVolumeAsync(Guid damCalculationId)
    {
        var dam = await _db.DamCalculations
            .FirstOrDefaultAsync(d => d.DamCalculationId == damCalculationId)
            ?? throw new InvalidOperationException($"DamCalculation {damCalculationId} not found.");

        decimal capacity = dam.CalculationMethod switch
        {
            "Method1" => DamVolumeCalculator.ComputeMethod1(
                dam.WallLength ?? throw new InvalidOperationException("WallLength is required for Method 1."),
                dam.Fetch ?? throw new InvalidOperationException("Fetch is required for Method 1."),
                dam.RiverDistance ?? throw new InvalidOperationException("RiverDistance is required for Method 1."),
                dam.ContourDifference ?? throw new InvalidOperationException("ContourDifference is required for Method 1."),
                dam.ShapeFactor ?? throw new InvalidOperationException("ShapeFactor is required for Method 1.")),
            "Method2" => DamVolumeCalculator.ComputeMethod2(
                dam.DamArea ?? throw new InvalidOperationException("DamArea is required for Method 2."),
                dam.DamDepth ?? throw new InvalidOperationException("DamDepth is required for Method 2."),
                dam.ShapeFactor ?? throw new InvalidOperationException("ShapeFactor is required for Method 2.")),
            _ => throw new InvalidOperationException($"Unknown CalculationMethod '{dam.CalculationMethod}'. Use 'Method1' or 'Method2'.")
        };

        dam.DamCapacity = capacity;
        await _db.SaveChangesAsync();
        return capacity;
    }

    public async Task<SfraResult> ComputeSfraAsync(Guid forestationId)
    {
        var f = await _db.Forestations
            .FirstOrDefaultAsync(x => x.ForestationId == forestationId)
            ?? throw new InvalidOperationException($"Forestation {forestationId} not found.");

        var speciesName = f.Specie ?? "";
        var speciesRate = await _db.SfraSpeciesRates
            .Where(r => r.SpeciesName == speciesName)
            .Select(r => r.RateM3PerHaPerAnnum)
            .FirstOrDefaultAsync();

        if (speciesRate == 0)
            throw new InvalidOperationException($"No SfraSpeciesRate found for species '{speciesName}'. Seed reference data first.");

        var result = SfraCalculator.Compute(
            f.Pre1972Hectares,
            f.SFRAPermitHectares,
            f.QualifyPeriodSFRAHectares ?? 0m,
            speciesRate);

        f.ELUHectares = result.EluHa;
        f.ELUVolume = result.EluVolume;
        f.LawfulHectares = result.LawfulHa;
        f.LawfulVolume = result.LawfulVolume;
        f.UnlawfulHectares = result.UnlawfulHa;
        f.UnlawfulVolume = result.UnlawfulVolume;
        await _db.SaveChangesAsync();
        return result;
    }
}
```

- [ ] **Step 6.5 — Register `ICalculatorService` in `Program.cs`**

Find the section with other `AddScoped` registrations and add:
```csharp
builder.Services.AddScoped<ICalculatorService, CalculatorService>();
```

- [ ] **Step 6.6 — Run tests — expect green**

```bash
dotnet test --filter "CalculatorServiceTests" 2>&1 | tail -5
```

Expected: `3 passed, 0 failed`

- [ ] **Step 6.7 — Full build + all tests**

```bash
dotnet build && dotnet test --no-build 2>&1 | tail -10
```

Expected: `Build succeeded`, all previously passing tests still pass.

- [ ] **Step 6.8 — Commit**

```bash
git add Services/Calculator/ Program.cs Tests/Services/Calculator/CalculatorServiceTests.cs
git commit -m "feat(calculator): CalculatorService — orchestrates SAPWAT, dam, SFRA compute + save (Wave 2a Task 6)"
```

---

## Task 7 — Dam Calculation UI (Method Selector + Calculate Button)

### Files
- **Modify:** `Controllers/DamCalculationController.cs` — add `Calculate` POST action
- **Modify:** `Views/DamCalculation/Edit.cshtml` — method selector + input fields + Calculate button

---

- [ ] **Step 7.1 — Read `Controllers/DamCalculationController.cs`** to confirm constructor dependencies and the existing Edit POST action pattern.

- [ ] **Step 7.2 — Add `ICalculatorService` to the `DamCalculationController` constructor**

Read the controller first, then add `ICalculatorService` to the constructor parameter list and field:
```csharp
private readonly ICalculatorService _calculator;
// in constructor:
_calculator = calculator;
```

Also add `using dwa_ver_val.Services.Calculator;` at the top if not already present.

- [ ] **Step 7.3 — Add `Calculate` POST action to `DamCalculationController`**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = DwsPolicies.CanCapture)]
public async Task<IActionResult> Calculate(Guid id)
{
    try
    {
        var capacity = await _calculator.ComputeDamVolumeAsync(id);
        TempData["Success"] = $"Dam capacity calculated: {capacity:N0} m³";
    }
    catch (InvalidOperationException ex)
    {
        TempData["Error"] = ex.Message;
    }
    return RedirectToAction(nameof(Edit), new { id });
}
```

- [ ] **Step 7.4 — Read `Views/DamCalculation/Edit.cshtml`** to understand the current layout.

- [ ] **Step 7.5 — Update `Views/DamCalculation/Edit.cshtml`**

Add a new section after the existing fields. Insert the method selector and input groups before the Save button:

```cshtml
<div class="form-section-title">Appendix D Calculation Inputs</div>

<div class="form-group">
    <label class="form-label">Calculation Method</label>
    <select id="calcMethod" name="CalculationMethod" class="form-select" onchange="toggleMethod()">
        <option value="">-- Select Method --</option>
        <option value="Method1" @(Model.CalculationMethod == "Method1" ? "selected" : "")>Method 1 — Wall Length</option>
        <option value="Method2" @(Model.CalculationMethod == "Method2" ? "selected" : "")>Method 2 — Area</option>
    </select>
</div>

<div id="method1-fields" style="display:none;">
    <div class="form-row">
        <div class="form-group">
            <label class="form-label">Wall Length (m)</label>
            <input type="number" step="0.01" name="WallLength" value="@Model.WallLength" class="form-input" />
        </div>
        <div class="form-group">
            <label class="form-label">Fetch (m)</label>
            <input type="number" step="0.01" name="Fetch" value="@Model.Fetch" class="form-input" />
        </div>
    </div>
    <div class="form-row">
        <div class="form-group">
            <label class="form-label">River Distance R1 (m)</label>
            <input type="number" step="0.01" name="RiverDistance" value="@Model.RiverDistance" class="form-input" />
        </div>
        <div class="form-group">
            <label class="form-label">Contour Difference C1 (m)</label>
            <input type="number" step="0.01" name="ContourDifference" value="@Model.ContourDifference" class="form-input" />
        </div>
    </div>
</div>

<div id="method2-fields" style="display:none;">
    <div class="form-row">
        <div class="form-group">
            <label class="form-label">Dam Area (ha)</label>
            <input type="number" step="0.01" name="DamArea" value="@Model.DamArea" class="form-input" />
        </div>
        <div class="form-group">
            <label class="form-label">Dam Depth (m)</label>
            <input type="number" step="0.01" name="DamDepth" value="@Model.DamDepth" class="form-input" />
        </div>
    </div>
</div>

<div class="form-group" id="shape-factor-group" style="display:none;">
    <label class="form-label">Shape Factor</label>
    <select name="ShapeFactor" class="form-select">
        <option value="">-- Select Shape --</option>
        <option value="0.33" @(Model.ShapeFactor == 0.33m ? "selected" : "")>0.33 — Ravine (triangle)</option>
        <option value="0.40" @(Model.ShapeFactor == 0.40m ? "selected" : "")>0.40 — Square with bends</option>
        <option value="0.50" @(Model.ShapeFactor == 0.50m ? "selected" : "")>0.50 — Circular</option>
    </select>
</div>

@if (Model.DamCapacity > 0)
{
    <div style="margin: 12px 0; padding: 10px 14px; background: #f0fdf4; border: 1px solid #86efac; border-radius: 6px; font-size: 13px; color: #166534;">
        <strong>Calculated capacity:</strong> @Model.DamCapacity.ToString("N0") m³
        @if (!string.IsNullOrEmpty(Model.CalculationMethod))
        {
            <span> (via @Model.CalculationMethod)</span>
        }
    </div>
}
```

Add the Calculate button alongside the Save button and a small JavaScript block at the bottom:

```cshtml
<form asp-action="Calculate" asp-route-id="@Model.DamCalculationId" method="post" style="display:inline;">
    @Html.AntiForgeryToken()
    <button type="submit" class="btn btn-outline">Calculate Capacity</button>
</form>
```

Add at the very bottom of the file (before closing tags):
```cshtml
@section Scripts {
    <script>
        function toggleMethod() {
            var v = document.getElementById('calcMethod').value;
            document.getElementById('method1-fields').style.display = v === 'Method1' ? '' : 'none';
            document.getElementById('method2-fields').style.display = v === 'Method2' ? '' : 'none';
            document.getElementById('shape-factor-group').style.display = v ? '' : 'none';
        }
        toggleMethod(); // run on load to restore state
    </script>
}
```

- [ ] **Step 7.6 — Build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7.7 — Commit**

```bash
git add Controllers/DamCalculationController.cs Views/DamCalculation/Edit.cshtml
git commit -m "feat(calculator): dam calculation UI — method selector + calculate button (Wave 2a Task 7)"
```

---

## Task 8 — FieldAndCrop UI (SAPWAT Calculate Button)

### Files
- **Modify:** `Controllers/FieldAndCropController.cs` — add `ICalculatorService` + `Calculate` POST
- **Modify:** `Views/FieldAndCrop/Edit.cshtml` — add SAPWAT result display + Calculate button

---

- [ ] **Step 8.1 — Read `Controllers/FieldAndCropController.cs`** to confirm constructor and Edit action shape.

- [ ] **Step 8.2 — Add `ICalculatorService` to `FieldAndCropController` constructor**

```csharp
private readonly ICalculatorService _calculator;
// add to constructor parameter list: ICalculatorService calculator
// in constructor body: _calculator = calculator;
```

- [ ] **Step 8.3 — Add `Calculate` POST action**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = DwsPolicies.CanCapture)]
public async Task<IActionResult> Calculate(Guid id)
{
    try
    {
        var rate = await _calculator.ComputeSapwatAsync(id);
        TempData["Success"] = $"SAPWAT rate calculated: {rate:N1} mm/ha/a";
    }
    catch (InvalidOperationException ex)
    {
        TempData["Error"] = ex.Message;
    }
    return RedirectToAction(nameof(Edit), new { id });
}
```

- [ ] **Step 8.4 — Read `Views/FieldAndCrop/Edit.cshtml`** to find where `SAPWATCalculationResult` is displayed.

- [ ] **Step 8.5 — Update the SAPWAT result field in `Views/FieldAndCrop/Edit.cshtml`**

Replace the static input for `SAPWATCalculationResult` with a read-only display + Calculate button:

```cshtml
<div class="form-group">
    <div class="form-label">SAPWAT Result (mm/ha/a)</div>
    <div style="display:flex; align-items:center; gap:12px;">
        <input asp-for="SAPWATCalculationResult" class="form-input" style="width:160px;" readonly />
        <form asp-action="Calculate" asp-route-id="@Model.FieldAndCropId" method="post" style="margin:0;">
            @Html.AntiForgeryToken()
            <button type="submit" class="btn btn-outline btn-sm">Run SAPWAT</button>
        </form>
    </div>
    <div style="font-size:12px; color:var(--dws-text-muted); margin-top:4px;">
        Computed from crop water rate × rotation factor. Requires crop to be seeded in reference data.
    </div>
</div>
```

- [ ] **Step 8.6 — Build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 8.7 — Commit**

```bash
git add Controllers/FieldAndCropController.cs Views/FieldAndCrop/Edit.cshtml
git commit -m "feat(calculator): FieldAndCrop SAPWAT calculate button (Wave 2a Task 8)"
```

---

## Task 9 — Forestation SFRA Calculate Button

### Files
- **Modify:** `Controllers/ForestationController.cs` — add `ICalculatorService` + `CalculateSfra` POST
- **Modify:** `Views/Forestation/Edit.cshtml` — add Calculate SFRA button + result display

---

- [ ] **Step 9.1 — Read `Controllers/ForestationController.cs`** to confirm constructor shape.

- [ ] **Step 9.2 — Add `ICalculatorService` to `ForestationController` constructor**

```csharp
private readonly ICalculatorService _calculator;
// add to constructor: ICalculatorService calculator
// in body: _calculator = calculator;
```

- [ ] **Step 9.3 — Add `CalculateSfra` POST action**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = DwsPolicies.CanCapture)]
public async Task<IActionResult> CalculateSfra(Guid id)
{
    try
    {
        var result = await _calculator.ComputeSfraAsync(id);
        TempData["Success"] = $"SFRA calculated: {result.EluHa:N2} ha ELU ({result.EluVolume:N0} m³/a), {result.UnlawfulHa:N2} ha unlawful.";
    }
    catch (InvalidOperationException ex)
    {
        TempData["Error"] = ex.Message;
    }
    return RedirectToAction(nameof(Edit), new { id });
}
```

- [ ] **Step 9.4 — Read `Views/Forestation/Edit.cshtml`** to find where ELU/Lawful/Unlawful fields are displayed.

- [ ] **Step 9.5 — Add Calculate SFRA button to `Views/Forestation/Edit.cshtml`**

Add above the ELU result fields (read-only):
```cshtml
<div class="form-section-title" style="margin-top:20px;">SFRA ELU Determination</div>

<form asp-action="CalculateSfra" asp-route-id="@Model.ForestationId" method="post" style="margin-bottom:12px;">
    @Html.AntiForgeryToken()
    <button type="submit" class="btn btn-outline">Calculate SFRA ELU</button>
    <span style="font-size:12px; color:var(--dws-text-muted); margin-left:8px;">
        Computes from Pre-1972 ha, SFRA permit ha, qualifying ha, and species rate.
    </span>
</form>
```

Make the ELU/Lawful/Unlawful fields read-only (they are now computed, not manually entered):
```cshtml
<input asp-for="ELUHectares" class="form-input" readonly />
<input asp-for="ELUVolume" class="form-input" readonly />
<input asp-for="LawfulHectares" class="form-input" readonly />
<input asp-for="LawfulVolume" class="form-input" readonly />
<input asp-for="UnlawfulHectares" class="form-input" readonly />
<input asp-for="UnlawfulVolume" class="form-input" readonly />
```

- [ ] **Step 9.6 — Final build + full test run**

```bash
dotnet build && dotnet test --no-build 2>&1 | tail -15
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, all tests pass (no new failures).

- [ ] **Step 9.7 — Final commit**

```bash
git add Controllers/ForestationController.cs Views/Forestation/Edit.cshtml
git commit -m "feat(calculator): Forestation SFRA calculate button (Wave 2a Task 9)"
```

---

## Verification Checklist

After all 9 tasks complete, verify manually:

1. Navigate to a DamCalculation Edit page → Method 1 fields show when Method 1 selected; Method 2 fields show for Method 2.
2. Fill in Method 2 inputs (Area=2ha, Depth=3m, ShapeFactor=0.5) → click Calculate → shows "3,000 m³".
3. Navigate to a FieldAndCrop Edit page → click Run SAPWAT → `SAPWATCalculationResult` updates.
4. Navigate to a Forestation Edit page → click Calculate SFRA → ELU/Unlawful fields populate.
5. CP6 guard on the workflow panel clears after FieldAndCrop has `SAPWATCalculationResult > 0`.
6. `dotnet test` — no new failures.

---

## What Wave 2b (LawfulnessAssessmentService) Will Need From This Plan

Wave 2b reads:
- `FieldAndCrop.SAPWATCalculationResult` — set by Task 8
- `DamCalculation.DamCapacity` — set by Task 7
- `Forestation.ELUHectares` / `ELUVolume` — set by Task 9
- `GwcaProclamationRule` records (already seeded in Wave 1)
- `Property.WaterManagementArea` (already present)

Wave 2b will produce a final `Entitlement` linked to `FileMaster` with `LawfulVolume`, `UnlawfulVolume`, and the legal basis applied.
