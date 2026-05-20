using Microsoft.EntityFrameworkCore;

public class SeedDataService
{
    private readonly ApplicationDBContext _context;

    public SeedDataService(ApplicationDBContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        // Dedup any existing duplicates BEFORE per-item seeding logic runs.
        // Order matters — children before parents to avoid FK constraint errors:
        //   WorkflowStates (no FK deps), then Crops → CropWaterRates, then SfraSpeciesRates.
        await DeduplicateExistingSeedRowsAsync();

        await SeedProvincesAsync();
        await SeedWaterManagementAreasAsync();
        await SeedWorkflowStatesAsync();
        await SeedLetterTypesAsync();
        await SeedAuthorisationTypesAsync();
        await SeedPeriodsAsync();
        // BUG-003: GWCAs must be seeded BEFORE proclamation rules (rules FK → GWCA).
        await SeedGovernmentWaterControlAreasAsync();
        await SeedGwcaProclamationRulesAsync();
        await SeedEntitlementTypesAsync();
        await SeedCustomerTypesAsync();
        // BUG-004: WaterSources / IrrigationSystems required by FieldAndCrop Create dropdowns.
        await SeedWaterSourcesAsync();
        await SeedIrrigationSystemsAsync();
        await SeedSampleCasesAsync();
        await SeedCropsAsync();
        await SeedCalculatorReferenceDataAsync();
    }

    // ── Deduplication of pre-existing duplicate seed rows ─────────────────
    // Earlier seed code used `if (!await _context.X.AnyAsync())` on the whole
    // table. Under concurrent app startup, multiple replicas could pass the
    // check before any commit and each insert the full set, producing
    // duplicates (typically 5×). Per-item idempotency below prevents future
    // duplicates; this method cleans up rows that already drifted.
    //
    // Strategy: for each natural key, keep the row with the lowest GUID
    // (deterministic, no clock dependency) and delete the rest.
    // Order matters — dedup parents BEFORE children to satisfy FK constraints
    // (Crops before CropWaterRates).
    private async Task DeduplicateExistingSeedRowsAsync()
    {
        // EF Core cannot translate `GroupBy + SelectMany + Skip` to SQL, so we
        // materialise the (key, id) pairs first and compute the keep/discard
        // partition in memory. These tables are all small lookups (<1000 rows
        // even in the duplicated state) so the round-trip is cheap.
        //
        // Cascade strategy: real DBs may already have business rows (Workflow
        // instances, FieldAndCrops, etc.) pointing at duplicate parent rows.
        // We REPARENT child FKs onto the kept row before deleting duplicates,
        // so existing business data is preserved.

        // 1. WorkflowStates — natural key: StateName.
        //    Children: WorkflowStepRecord.WorkflowStateId, WorkflowInstance.CurrentWorkflowStateId.
        var stateRows = await _context.WorkflowStates
            .Select(s => new { s.WorkflowStateId, s.StateName })
            .ToListAsync();
        var stateGroups = stateRows
            .GroupBy(s => s.StateName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (stateGroups.Count > 0)
        {
            // For each group: keep the lowest GUID; reparent children to it; delete the rest.
            var dupStateIds = new List<Guid>();
            foreach (var grp in stateGroups)
            {
                var ordered = grp.OrderBy(s => s.WorkflowStateId).ToList();
                var keepId = ordered[0].WorkflowStateId;
                var discardIds = ordered.Skip(1).Select(s => s.WorkflowStateId).ToList();
                dupStateIds.AddRange(discardIds);

                await _context.WorkflowStepRecords
                    .Where(r => discardIds.Contains(r.WorkflowStateId))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.WorkflowStateId, keepId));
                await _context.WorkflowInstances
                    .Where(i => discardIds.Contains(i.CurrentWorkflowStateId))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.CurrentWorkflowStateId, keepId));
            }
            await _context.WorkflowStates
                .Where(s => dupStateIds.Contains(s.WorkflowStateId))
                .ExecuteDeleteAsync();
        }

        // 2. Crops — natural key: CropName.
        //    Children: CropWaterRate.CropId, FieldAndCrop.CropId (shadow FK).
        //    Must dedup BEFORE CropWaterRates so the rate dedup below sees a clean parent set.
        var cropRows = await _context.Crops
            .Select(c => new { c.CropId, c.CropName })
            .ToListAsync();
        var cropGroups = cropRows
            .GroupBy(c => c.CropName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (cropGroups.Count > 0)
        {
            var dupCropIds = new List<Guid>();
            foreach (var grp in cropGroups)
            {
                var ordered = grp.OrderBy(c => c.CropId).ToList();
                var keepId = ordered[0].CropId;
                var discardIds = ordered.Skip(1).Select(c => c.CropId).ToList();
                dupCropIds.AddRange(discardIds);

                // Reparent CropWaterRates onto the kept Crop. The (CropId,IrrigSysId)
                // dedup pass that runs next will collapse any duplicates this creates.
                await _context.CropWaterRates
                    .Where(r => discardIds.Contains(r.CropId))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.CropId, keepId));

                // Reparent FieldAndCrops onto the kept Crop. CropId is a shadow FK
                // (no scalar property on the entity), so update via the shadow column.
                var fieldAndCrops = await _context.FieldAndCrops
                    .Where(f => discardIds.Contains(EF.Property<Guid>(f, "CropId")))
                    .ToListAsync();
                foreach (var fac in fieldAndCrops)
                {
                    _context.Entry(fac).Property("CropId").CurrentValue = keepId;
                }
                if (fieldAndCrops.Count > 0)
                    await _context.SaveChangesAsync();
            }
            await _context.Crops
                .Where(c => dupCropIds.Contains(c.CropId))
                .ExecuteDeleteAsync();
        }

        // 3. CropWaterRates — composite natural key: CropId + IrrigationSystemId
        //    (IrrigationSystemId may be null = "applies to all systems").
        //    No outgoing FKs from other tables to CropWaterRate, so safe to delete.
        var rateRows = await _context.CropWaterRates
            .Select(r => new { r.CropWaterRateId, r.CropId, r.IrrigationSystemId })
            .ToListAsync();
        var dupRateIds = rateRows
            .GroupBy(r => new { r.CropId, r.IrrigationSystemId })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(r => r.CropWaterRateId).Skip(1).Select(r => r.CropWaterRateId))
            .ToList();
        if (dupRateIds.Count > 0)
        {
            await _context.CropWaterRates
                .Where(r => dupRateIds.Contains(r.CropWaterRateId))
                .ExecuteDeleteAsync();
        }

        // 4. SfraSpeciesRates — natural key: SpeciesName.
        //    No outgoing FKs from other tables, so safe to delete.
        var sfraRows = await _context.SfraSpeciesRates
            .Select(r => new { r.SfraSpeciesRateId, r.SpeciesName })
            .ToListAsync();
        var dupSfraIds = sfraRows
            .GroupBy(r => r.SpeciesName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(r => r.SfraSpeciesRateId).Skip(1).Select(r => r.SfraSpeciesRateId))
            .ToList();
        if (dupSfraIds.Count > 0)
        {
            await _context.SfraSpeciesRates
                .Where(r => dupSfraIds.Contains(r.SfraSpeciesRateId))
                .ExecuteDeleteAsync();
        }

        // 5. GovernmentWaterControlAreas — natural key: GovernmentWaterControlAreaName.
        //    Children: GwcaProclamationRule.WaterControlAreaId, Property.WaterControlAreaId
        //    (nullable), LawfulnessAssessmentResult.GwcaId (nullable).
        var gwcaRows = await _context.GovernmentWaterControlAreas
            .Select(g => new { g.WaterControlAreaId, g.GovernmentWaterControlAreaName })
            .ToListAsync();
        var gwcaGroups = gwcaRows
            .GroupBy(g => g.GovernmentWaterControlAreaName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (gwcaGroups.Count > 0)
        {
            var dupGwcaIds = new List<Guid>();
            foreach (var grp in gwcaGroups)
            {
                var ordered = grp.OrderBy(g => g.WaterControlAreaId).ToList();
                var keepId = ordered[0].WaterControlAreaId;
                var discardIds = ordered.Skip(1).Select(g => g.WaterControlAreaId).ToList();
                dupGwcaIds.AddRange(discardIds);

                // Reparent children. For GwcaProclamationRules: if reparenting
                // collides (multiple GWCAs each had the same rules), prefer to
                // delete the duplicate rules since the kept GWCA already has
                // its canonical rule set after the rules seeder runs.
                await _context.GwcaProclamationRules
                    .Where(r => discardIds.Contains(r.WaterControlAreaId))
                    .ExecuteDeleteAsync();
                await _context.Properties
                    .Where(p => p.WaterControlAreaId != null && discardIds.Contains(p.WaterControlAreaId.Value))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.WaterControlAreaId, (Guid?)keepId));
                await _context.LawfulnessAssessmentResults
                    .Where(l => l.GwcaId != null && discardIds.Contains(l.GwcaId.Value))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.GwcaId, (Guid?)keepId));
            }
            await _context.GovernmentWaterControlAreas
                .Where(g => dupGwcaIds.Contains(g.WaterControlAreaId))
                .ExecuteDeleteAsync();
        }

        // 6. WaterSources — natural key: WaterSourceName.
        //    Children: Irrigation.WaterSourceId, FieldAndCrop.WaterSourceId (shadow FK).
        var wsRows = await _context.WaterSources
            .Select(w => new { w.WaterSourceId, w.WaterSourceName })
            .ToListAsync();
        var wsGroups = wsRows
            .GroupBy(w => w.WaterSourceName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (wsGroups.Count > 0)
        {
            var dupWsIds = new List<Guid>();
            foreach (var grp in wsGroups)
            {
                var ordered = grp.OrderBy(w => w.WaterSourceId).ToList();
                var keepId = ordered[0].WaterSourceId;
                var discardIds = ordered.Skip(1).Select(w => w.WaterSourceId).ToList();
                dupWsIds.AddRange(discardIds);

                await _context.Irrigations
                    .Where(i => discardIds.Contains(i.WaterSourceId))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.WaterSourceId, keepId));

                // FieldAndCrop.WaterSourceId is a shadow FK — update via shadow column.
                var fields = await _context.FieldAndCrops
                    .Where(f => discardIds.Contains(EF.Property<Guid>(f, "WaterSourceId")))
                    .ToListAsync();
                foreach (var f in fields)
                {
                    _context.Entry(f).Property("WaterSourceId").CurrentValue = keepId;
                }
                if (fields.Count > 0)
                    await _context.SaveChangesAsync();
            }
            await _context.WaterSources
                .Where(w => dupWsIds.Contains(w.WaterSourceId))
                .ExecuteDeleteAsync();
        }

        // 7a. GwcaProclamationRules — natural key: (WaterControlAreaId, RuleCode).
        //     Concurrent seeding inflates these in lockstep with GWCAs. Done
        //     AFTER GWCA dedup so we work against a clean parent set.
        var ruleRows = await _context.GwcaProclamationRules
            .Select(r => new { r.RuleId, r.WaterControlAreaId, r.RuleCode })
            .ToListAsync();
        var dupRuleIds = ruleRows
            .GroupBy(r => new { r.WaterControlAreaId, r.RuleCode })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(r => r.RuleId).Skip(1).Select(r => r.RuleId))
            .ToList();
        if (dupRuleIds.Count > 0)
        {
            await _context.GwcaProclamationRules
                .Where(r => dupRuleIds.Contains(r.RuleId))
                .ExecuteDeleteAsync();
        }

        // 7. IrrigationSystems — natural key: IrrigationSystemName.
        //    Children: CropWaterRate.IrrigationSystemId (nullable), FieldAndCrop.IrrigationSystemId (shadow FK, nullable).
        var isRows = await _context.IrrigationSystems
            .Select(s => new { s.IrrigationSystemId, s.IrrigationSystemName })
            .ToListAsync();
        var isGroups = isRows
            .GroupBy(s => s.IrrigationSystemName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (isGroups.Count > 0)
        {
            var dupIsIds = new List<Guid>();
            foreach (var grp in isGroups)
            {
                var ordered = grp.OrderBy(s => s.IrrigationSystemId).ToList();
                var keepId = ordered[0].IrrigationSystemId;
                var discardIds = ordered.Skip(1).Select(s => s.IrrigationSystemId).ToList();
                dupIsIds.AddRange(discardIds);

                await _context.CropWaterRates
                    .Where(r => r.IrrigationSystemId != null && discardIds.Contains(r.IrrigationSystemId.Value))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IrrigationSystemId, (Guid?)keepId));

                // FieldAndCrop.IrrigationSystemId is a shadow nullable FK.
                var fields = await _context.FieldAndCrops
                    .Where(f => discardIds.Contains(EF.Property<Guid?>(f, "IrrigationSystemId")!.Value))
                    .ToListAsync();
                foreach (var f in fields)
                {
                    _context.Entry(f).Property("IrrigationSystemId").CurrentValue = (Guid?)keepId;
                }
                if (fields.Count > 0)
                    await _context.SaveChangesAsync();
            }
            await _context.IrrigationSystems
                .Where(s => dupIsIds.Contains(s.IrrigationSystemId))
                .ExecuteDeleteAsync();
        }
    }

    // ── Government Water Control Areas (parent of GwcaProclamationRule) ──
    // BUG-003: rule seeding looks up "Blyde River" GWCA. If no GWCA exists,
    // the lookup returns null and rules silently never get inserted, leaving
    // the Property Edit dropdown empty. Seed canonical GWCAs first.
    private async Task SeedGovernmentWaterControlAreasAsync()
    {
        var gwcas = new[]
        {
            new
            {
                Name = "Blyde River",
                Gazette = "GN 180 of 10 July 1970",
                Proclaimed = new DateOnly(1970, 7, 10),
            },
        };

        // Per-name idempotency: only insert rows whose name does not yet exist.
        var existingNames = await _context.GovernmentWaterControlAreas
            .Select(g => g.GovernmentWaterControlAreaName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var g in gwcas)
        {
            if (existingSet.Contains(g.Name)) continue;

            _context.GovernmentWaterControlAreas.Add(new GovernmentWaterControlArea
            {
                WaterControlAreaId = Guid.NewGuid(),
                GovernmentWaterControlAreaName = g.Name,
                GovernmentGazetteReference = g.Gazette,
                ProclamationDate = g.Proclaimed,
            });
            added = true;
        }

        if (added)
            await _context.SaveChangesAsync();
    }

    // ── Water Sources (lookup for FieldAndCrop / Irrigation dropdowns) ───
    // BUG-004: FieldAndCrop Create has a required WaterSource dropdown.
    // Without seed data the form cannot be submitted on a fresh install.
    private async Task SeedWaterSourcesAsync()
    {
        var sources = new (string Name, WaterSourceType Type)[]
        {
            ("River",            WaterSourceType.SURFACE),
            ("Borehole",         WaterSourceType.BOREHOLE),
            ("Dam",              WaterSourceType.SURFACE),
            ("Spring",           WaterSourceType.SURFACE),
            ("Irrigation Canal", WaterSourceType.SURFACE),
            ("Wetland",          WaterSourceType.SURFACE),
        };

        var existingNames = await _context.WaterSources
            .Select(w => w.WaterSourceName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var (name, type) in sources)
        {
            if (existingSet.Contains(name)) continue;

            _context.WaterSources.Add(new WaterSource
            {
                WaterSourceId = Guid.NewGuid(),
                WaterSourceName = name,
                WaterSourceType = type,
            });
            added = true;
        }

        if (added)
            await _context.SaveChangesAsync();
    }

    // ── Irrigation Systems (lookup for FieldAndCrop dropdown) ────────────
    // BUG-004: FieldAndCrop Create has a required IrrigationSystem dropdown.
    private async Task SeedIrrigationSystemsAsync()
    {
        var systems = new[]
        {
            "Drip",
            "Sprinkler",
            "Flood/Furrow",
            "Centre Pivot",
            "Micro-irrigation",
        };

        var existingNames = await _context.IrrigationSystems
            .Select(s => s.IrrigationSystemName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var name in systems)
        {
            if (existingSet.Contains(name)) continue;

            _context.IrrigationSystems.Add(new IrrigationSystem
            {
                IrrigationSystemId = Guid.NewGuid(),
                IrrigationSystemName = name,
            });
            added = true;
        }

        if (added)
            await _context.SaveChangesAsync();
    }

    // ── CustomerTypes ─────────────────────────────────────────────────────

    private async Task SeedCustomerTypesAsync()
    {
        if (await _context.CustomerTypes.AnyAsync())
            return;

        var types = new[]
        {
            "Individual",
            "Company / Legal Entity",
            "Government",
            "Communal / Tribal Authority",
            "Irrigation Board Member",
            "Other"
        };

        _context.CustomerTypes.AddRange(types.Select(name => new CustomerType
        {
            Id = Guid.NewGuid(),
            CustomerTypeName = name
        }));

        await _context.SaveChangesAsync();
    }

    // ── 1. Provinces (9) ──────────────────────────────────────────────

    private async Task SeedProvincesAsync()
    {
        if (await _context.Provinces.AnyAsync())
            return;

        var provinces = new List<Province>
        {
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Eastern Cape",   ProvinceCode = "EC"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Free State",     ProvinceCode = "FS"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Gauteng",        ProvinceCode = "GP"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "KwaZulu-Natal",  ProvinceCode = "KZN" },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Limpopo",        ProvinceCode = "LP"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Mpumalanga",     ProvinceCode = "MP"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "North West",     ProvinceCode = "NW"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Northern Cape",  ProvinceCode = "NC"  },
            new Province { ProvinceId = Guid.NewGuid(), ProvinceName = "Western Cape",   ProvinceCode = "WC"  },
        };

        _context.Provinces.AddRange(provinces);
        await _context.SaveChangesAsync();
    }

    // ── 2. Water Management Areas (11 WMAs, 2012 redetermination) ────

    private async Task SeedWaterManagementAreasAsync()
    {
        if (await _context.WaterManagementAreas.AnyAsync())
            return;

        // Retrieve seeded provinces by code for FK assignment
        var provinces = await _context.Provinces.ToDictionaryAsync(p => p.ProvinceCode);

        var wmas = new List<WaterManagementArea>
        {
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Limpopo",              WmaCode = "1",
                ProvinceId = provinces["LP"].ProvinceId
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Olifants",             WmaCode = "2",
                ProvinceId = provinces["LP"].ProvinceId   // LP/MP — primary LP
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Inkomati-Usuthu",      WmaCode = "3",
                ProvinceId = provinces["MP"].ProvinceId
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Pongola-Mtamvuna",     WmaCode = "4",
                ProvinceId = provinces["KZN"].ProvinceId
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Vaal",                 WmaCode = "5",
                ProvinceId = provinces["GP"].ProvinceId   // GP/FS/NW — primary GP
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Orange",               WmaCode = "6",
                ProvinceId = provinces["FS"].ProvinceId   // FS/NC — primary FS
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Mzimvubu-Tsitsikamma", WmaCode = "7",
                ProvinceId = provinces["EC"].ProvinceId
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Breede-Gouritz",       WmaCode = "8",
                ProvinceId = provinces["WC"].ProvinceId
            },
            new WaterManagementArea
            {
                WmaId = Guid.NewGuid(), WmaName = "Berg-Olifants",        WmaCode = "9",
                ProvinceId = provinces["WC"].ProvinceId
            },
        };

        _context.WaterManagementAreas.AddRange(wmas);
        await _context.SaveChangesAsync();
    }

    // ── 3. Workflow States (CP1 sub-steps + CP2–CP9 + S35 sub-states) ─

    private async Task SeedWorkflowStatesAsync()
    {
        // Canonical workflow-state catalogue. Treated as the single source of
        // truth: any row missing in DB is inserted; any row whose DisplayOrder
        // drifts from the canonical value is corrected.
        //
        // BUG-005: per-item idempotency (was bulk-AnyAsync on a virgin DB).
        // Concurrent startup could otherwise yield duplicate state rows.
        var canonical = new (string Name, string Phase, int Order, bool Terminal)[]
        {
            // Phase: Inception (CP1 sub-steps)
            ("CP1_WARMSObtained",        "Inception",    1,  false),
            ("CP1_SatelliteImagery",     "Inception",    2,  false),
            ("CP1_DatabaseAudit",        "Inception",    3,  false),
            ("CP1_UnregisteredUsers",    "Inception",    4,  false),
            ("CP1_DatabaseAnalysis",     "Inception",    5,  false),
            ("CP1_InceptionReport",      "Inception",    6,  false),
            ("CP1_PublicParticipation",  "Inception",    7,  false),

            // Phase: Validation — Technical: did use EXIST, what EXTENT (CP2–CP6)
            ("CP2_SpatialInfo",          "Validation",   8,  false),
            ("CP3_WARMSEvaluation",      "Validation",   9,  false),
            ("CP4_AdditionalInfo",       "Validation",   10, false),
            ("CP5_GISAnalysis",          "Validation",   11, false),
            ("CP6_FieldCropSAPWAT",      "Validation",   12, false),

            // Phase: Verification — Legal: was use LAWFUL (CP7–CP9)
            ("CP7_ELUCalculated",        "Verification", 13, false),
            ("CP8_DamVolumes",           "Verification", 14, false),
            ("CP9_SFRACalculated",       "Verification", 15, false),

            // Phase: Verification — PRD CP12/CP13 (Pre-Public Review + Stakeholder Workshop)
            ("CP_PrePublicReview",       "Verification", 16, false),
            ("CP_StakeholderWorkshop",   "Verification", 17, false),

            // Phase: Verification — Section 35 letter sub-states (Track A)
            ("S35_Letter1Issued",            "Verification", 18, false),
            ("S35_Letter1Responded",         "Verification", 19, false),
            ("S35_Letter1ARequired",         "Verification", 20, false),
            ("S35_Letter1AIssued",           "Verification", 21, false),
            ("S35_Letter1AResponded",        "Verification", 22, false),
            ("S35_AdditionalInfoRequired",   "Verification", 23, false),
            ("S35_Letter2Issued",            "Verification", 24, false),
            ("S35_Letter2Responded",         "Verification", 25, false),
            ("S35_Letter2ARequired",         "Verification", 26, false),
            ("S35_Letter2AIssued",           "Verification", 27, false),
            ("S35_Letter3Issued",            "Verification", 28, false),
            ("S35_ELUConfirmed",             "Verification", 29, false),
            ("S35_UnlawfulUseFound",         "Verification", 30, false),
            ("S35_Letter4AIssued",           "Verification", 31, false),
            ("S35_Letter4And5Issued",        "Verification", 32, false),

            // Phase: Verification — Section 33 declaration sub-states (Tracks B & C)
            ("S33_2_ReadyForDeclaration",    "Verification", 33, false),
            ("S33_2_DeclarationIssued",      "Verification", 34, false),
            ("S33_3_DeclarationIssued",      "Verification", 35, false),

            // Terminal states
            ("Closed",                       "Verification", 36, true),
        };

        // Materialise existing rows once for efficient per-name lookup + drift
        // correction on DisplayOrder.
        var existing = await _context.WorkflowStates.ToListAsync();
        var existingByName = existing.ToDictionary(s => s.StateName, StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var (name, phase, order, terminal) in canonical)
        {
            if (existingByName.TryGetValue(name, out var row))
            {
                // Idempotent correction: keep DisplayOrder aligned to canonical.
                if (row.DisplayOrder != order)
                {
                    row.DisplayOrder = order;
                    changed = true;
                }
                continue;
            }

            _context.WorkflowStates.Add(new WorkflowState
            {
                WorkflowStateId = Guid.NewGuid(),
                StateName = name,
                Phase = phase,
                DisplayOrder = order,
                IsTerminal = terminal,
            });
            changed = true;
        }

        if (changed)
            await _context.SaveChangesAsync();
    }

    // ── 4. Letter Types ──────────────────────────────────────────────

    private async Task SeedLetterTypesAsync()
    {
        // LetterName is the canonical lookup key consumed by ILetterTemplate registrations
        // and FileMasterController.LetterActionMap. We use short codes (S35_L1, S33_2_Decl)
        // rather than human names so the same string round-trips between code and DB.
        // Idempotent: this runs every startup; missing rows are added; rows with the legacy
        // human names ("Letter 1" etc.) are auto-renamed to their canonical code.
        var canonical = new (string LegacyName, string Code, string Description, string NWASection)[]
        {
            ("Letter 1",                "S35_L1",      "S35(1) Notice to apply for verification",                              "S35(1)"),
            ("Letter 1A",               "S35_L1A",     "S53(1) Directive to apply for verification",                           "S53(1)"),
            ("Letter 2",                "S35_L2",      "S35(3)(a) Request for additional information",                         "S35(3)(a)"),
            ("Letter 2A",               "S35_L2A",     "S35(1) Directive to provide additional information",                   "S35(1)"),
            ("Letter 3",                "S35_L3",      "S35(4) Confirmation of extent and lawfulness of water use",            "S35(4)"),
            ("Letter 4A",               "S35_L4A",     "S53(1) Notice of intent to issue directive to stop unlawful use",      "S53(1)"),
            ("Letter 4 & 5",            "S35_L4_5",    "S53(1) Directive to stop unlawful water use",                          "S53(1)"),
            ("S33(2) Declaration",      "S33_2_Decl",  "Kader Asmal Declaration — confirms ELU for irrigation board scheduled area", "S33(2)"),
            ("S33(3)(a) Declaration",   "S33_3a_Decl", "Declaration of ELU on individual application — category A",            "S33(3)(a)"),
            ("S33(3)(b) Declaration",   "S33_3b_Decl", "Declaration of ELU on individual application — category B",            "S33(3)(b)"),
        };

        foreach (var row in canonical)
        {
            var existing = await _context.LetterTypes
                .FirstOrDefaultAsync(t => t.LetterName == row.LegacyName || t.LetterName == row.Code);
            if (existing is null)
            {
                _context.LetterTypes.Add(new LetterType
                {
                    LetterTypeId = Guid.NewGuid(),
                    LetterName = row.Code,
                    LetterDescription = row.Description,
                    NWASection = row.NWASection
                });
            }
            else if (existing.LetterName != row.Code)
            {
                existing.LetterName = row.Code;
            }
        }
        await _context.SaveChangesAsync();
    }

    // ── 5. Authorisation Types ───────────────────────────────────────

    private async Task SeedAuthorisationTypesAsync()
    {
        if (await _context.AuthorisationTypes.AnyAsync())
            return;

        var names = new[]
        {
            "Permit",
            "Section 32/33 Approval",
            "Transfer (Temporary)",
            "Transfer (Permanent)",
            "Ad-hoc Field Survey",
            "Water Court",
            "Other Water Act",
            "License",
            "General Authorisation",
            "Other"
        };

        var types = names.Select(name => new AuthorisationType
        {
            AuthorisationTypeId = Guid.NewGuid(),
            AuthorisationTypeName = name,
            Description = name
        }).ToList();

        _context.AuthorisationTypes.AddRange(types);
        await _context.SaveChangesAsync();
    }

    // ── 6. Periods ───────────────────────────────────────────────────

    private async Task SeedPeriodsAsync()
    {
        if (await _context.Periods.AnyAsync())
            return;

        // Single unified qualifying period per SFRA court order direction:
        // 1 October 1996 – 30 September 1998 for BOTH ground and surface water
        var periods = new List<Period>
        {
            new Period
            {
                PeriodId = Guid.NewGuid(),
                PeriodName = "Qualifying Period: 1 Oct 1996 - 30 Sep 1998"
            },
            new Period
            {
                PeriodId = Guid.NewGuid(),
                PeriodName = "Current Period"
            },
        };

        _context.Periods.AddRange(periods);
        await _context.SaveChangesAsync();
    }

    // ── 7. GWCA Proclamation Rules (example: Blyde River) ────────────

    private async Task SeedGwcaProclamationRulesAsync()
    {
        // BUG-005 (extended): per-item idempotency keyed on (WaterControlAreaId, RuleCode).
        // Old bulk `AnyAsync()` check let concurrent startup insert N copies.

        // Only seed if we have a GWCA to attach rules to
        var blydeRiver = await _context.GovernmentWaterControlAreas
            .FirstOrDefaultAsync(g => g.GovernmentWaterControlAreaName.Contains("Blyde"));

        if (blydeRiver == null)
            return;

        var existingRuleCodes = await _context.GwcaProclamationRules
            .Where(r => r.WaterControlAreaId == blydeRiver.WaterControlAreaId)
            .Select(r => r.RuleCode)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingRuleCodes, StringComparer.OrdinalIgnoreCase);

        var rules = new List<GwcaProclamationRule>
        {
            new GwcaProclamationRule
            {
                RuleId = Guid.NewGuid(),
                WaterControlAreaId = blydeRiver.WaterControlAreaId,
                RuleCode = "MAX_HECTARES",
                RuleDescription = "Maximum irrigable hectares per property (or 40% of irrigable land, whichever is greater)",
                NumericLimit = 30,
                Unit = "ha",
                GovernmentGazetteReference = "GN 180 of 10 July 1970",
                IsActive = true,
                EffectiveFrom = new DateOnly(1970, 7, 10)
            },
            new GwcaProclamationRule
            {
                RuleId = Guid.NewGuid(),
                WaterControlAreaId = blydeRiver.WaterControlAreaId,
                RuleCode = "MAX_IRRIGABLE_PCT",
                RuleDescription = "Maximum percentage of irrigable land for existing lawful irrigation above minimum allocation",
                NumericLimit = 53,
                Unit = "pct",
                GovernmentGazetteReference = "GN 180 of 10 July 1970",
                IsActive = true,
                EffectiveFrom = new DateOnly(1970, 7, 10)
            },
            new GwcaProclamationRule
            {
                RuleId = Guid.NewGuid(),
                WaterControlAreaId = blydeRiver.WaterControlAreaId,
                RuleCode = "MAX_VOLUME_PER_HA",
                RuleDescription = "Maximum rate of abstraction per hectare",
                NumericLimit = 9900,
                Unit = "m3/ha",
                GovernmentGazetteReference = "Proclamation",
                IsActive = true
            },
            new GwcaProclamationRule
            {
                RuleId = Guid.NewGuid(),
                WaterControlAreaId = blydeRiver.WaterControlAreaId,
                RuleCode = "MAX_STORAGE_PER_HA",
                RuleDescription = "Maximum storage capacity per hectare of irrigable land",
                NumericLimit = 5000,
                Unit = "m3/ha",
                GovernmentGazetteReference = "GN 2805 of 14 December 1979",
                IsActive = true,
                EffectiveFrom = new DateOnly(1979, 12, 14)
            },
            new GwcaProclamationRule
            {
                RuleId = Guid.NewGuid(),
                WaterControlAreaId = blydeRiver.WaterControlAreaId,
                RuleCode = "MAX_STORAGE_PER_PROPERTY",
                RuleDescription = "Maximum storage capacity per property",
                NumericLimit = 50000,
                Unit = "m3",
                GovernmentGazetteReference = "GN 2805 of 14 December 1979",
                IsActive = true,
                EffectiveFrom = new DateOnly(1979, 12, 14)
            },
        };

        // Per-item idempotency: only add rules whose RuleCode is not already
        // attached to this GWCA. Filters before AddRange to keep the add list
        // clean for the DbContext change tracker.
        var rulesToAdd = rules.Where(r => !existingSet.Contains(r.RuleCode)).ToList();
        if (rulesToAdd.Count > 0)
        {
            _context.GwcaProclamationRules.AddRange(rulesToAdd);
            await _context.SaveChangesAsync();
        }
    }

    // ── 8. Sample cases for demo ────────────────────────────────────

    private async Task SeedSampleCasesAsync()
    {
        if (await _context.FileMasters.AnyAsync())
            return;

        var mpumalanga = await _context.Provinces.SingleAsync(p => p.ProvinceCode == "MP");
        var inkomati = await _context.WaterManagementAreas.SingleAsync(w => w.WmaName == "Inkomati-Usuthu");

        // Catchment area if none exists
        var catchment = await _context.CatchmentAreas.FirstOrDefaultAsync(c => c.CatchmentCode == "X21A");
        if (catchment == null)
        {
            catchment = new CatchmentArea
            {
                CatchmentAreaId = Guid.NewGuid(),
                CatchmentCode = "X21A",
                CatchmentName = "Upper Komati Quaternary",
                WmaId = inkomati.WmaId,
            };
            _context.CatchmentAreas.Add(catchment);
        }

        // Org unit if none exists
        var orgUnit = await _context.OrganisationalUnits.FirstOrDefaultAsync(o => o.Name == "Mpumalanga Regional Office");
        if (orgUnit == null)
        {
            orgUnit = new OrganisationalUnit
            {
                OrgUnitId = Guid.NewGuid(),
                Name = "Mpumalanga Regional Office",
                Type = "Regional",
                ProvinceId = mpumalanga.ProvinceId,
                WmaId = inkomati.WmaId,
            };
            _context.OrganisationalUnits.Add(orgUnit);
        }

        var prop1 = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "DRN-123",
            SGCode = "T0HT00000000012300000",
            QuaternaryDrainage = "X21A",
            WmaId = inkomati.WmaId,
            CatchmentAreaId = catchment.CatchmentAreaId,
        };
        var prop2 = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "LWF-456",
            SGCode = "T0HT00000000045600000",
            QuaternaryDrainage = "X21A",
            WmaId = inkomati.WmaId,
            CatchmentAreaId = catchment.CatchmentAreaId,
        };
        _context.Properties.AddRange(prop1, prop2);

        await _context.SaveChangesAsync();

        var samples = new[]
        {
            new { Reg = "WARMS-2024-001", Farm = "Doornhoek",  FarmNo = 123, Portion = "0", Prop = prop1, TargetState = "CP1_WARMSObtained" },
            new { Reg = "WARMS-2024-002", Farm = "Leeuwfontein", FarmNo = 456, Portion = "1", Prop = prop2, TargetState = "CP5_GISAnalysis" },
            new { Reg = "WARMS-2024-003", Farm = "Doornhoek",  FarmNo = 123, Portion = "2", Prop = prop1, TargetState = "CP9_SFRACalculated" },
        };

        foreach (var s in samples)
        {
            var fm = new FileMaster
            {
                FileMasterId = Guid.NewGuid(),
                RegistrationNumber = s.Reg,
                CaseNumber = $"VV-2026-{s.Reg.Substring(s.Reg.Length - 3)}",
                PropertyId = s.Prop.PropertyId,
                OrgUnitId = orgUnit.OrgUnitId,
                CatchmentAreaId = catchment.CatchmentAreaId,
                SurveyorGeneralCode = s.Prop.SGCode!,
                PrimaryCatchment = "X",
                QuaternaryCatchment = "X21A",
                FarmName = s.Farm,
                FarmNumber = s.FarmNo,
                RegistrationDivision = "JR",
                FarmPortion = s.Portion,
                FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
                AssessmentTrack = "S35_Verification",
                ValidationStatusName = s.TargetState == "CP1_WARMSObtained" ? "Not Commenced" : "In Process",
                RegisteredForTakingWater = true,
                RegisteredForStoring = false,
                RegisteredForForestation = false,
            };
            _context.FileMasters.Add(fm);
            await _context.SaveChangesAsync();

            // Build workflow instance inline (avoid calling WorkflowService from here)
            var targetState = await _context.WorkflowStates.SingleAsync(w => w.StateName == s.TargetState);
            var firstState = await _context.WorkflowStates.OrderBy(w => w.DisplayOrder).FirstAsync();

            var instance = new WorkflowInstance
            {
                WorkflowInstanceId = Guid.NewGuid(),
                FileMasterId = fm.FileMasterId,
                CurrentWorkflowStateId = targetState.WorkflowStateId,
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
            };
            _context.WorkflowInstances.Add(instance);

            // Synthesise history: Completed step per state from first up to (but not including) target, then InProgress for target.
            var traversed = await _context.WorkflowStates
                .Where(w => w.DisplayOrder < targetState.DisplayOrder)
                .OrderBy(w => w.DisplayOrder)
                .ToListAsync();

            var baseTime = DateTime.UtcNow.AddDays(-10);
            for (int i = 0; i < traversed.Count; i++)
            {
                _context.WorkflowStepRecords.Add(new WorkflowStepRecord
                {
                    WorkflowStepRecordId = Guid.NewGuid(),
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowStateId = traversed[i].WorkflowStateId,
                    StepStatus = "Completed",
                    StartedDate = baseTime.AddHours(i),
                    CompletedDate = baseTime.AddHours(i + 1),
                });
            }
            _context.WorkflowStepRecords.Add(new WorkflowStepRecord
            {
                WorkflowStepRecordId = Guid.NewGuid(),
                WorkflowInstanceId = instance.WorkflowInstanceId,
                WorkflowStateId = targetState.WorkflowStateId,
                StepStatus = "InProgress",
                StartedDate = baseTime.AddHours(traversed.Count + 1),
            });

            fm.WorkflowInstanceId = instance.WorkflowInstanceId;
            await _context.SaveChangesAsync();
        }
    }

    // ── 9. Calculator reference data (SFRA species rates + crop water rates) ─

    private async Task SeedCropsAsync()
    {
        // BUG-005: per-item idempotency (was bulk-AnyAsync). Concurrent startup
        // could otherwise yield duplicate inserts across replicas.
        var cropNames = new[]
        {
            "Maize", "Wheat", "Sugarcane", "Soybean", "Sunflower",
            "Groundnut", "Cotton", "Lucerne", "Pasture", "Vegetables",
            "Citrus", "Grapes", "Stone fruit", "Other",
        };

        var existingNames = await _context.Crops
            .Select(c => c.CropName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var name in cropNames)
        {
            if (existingSet.Contains(name)) continue;
            _context.Crops.Add(new Crop { CropId = Guid.NewGuid(), CropName = name });
            added = true;
        }

        if (added)
            await _context.SaveChangesAsync();
    }

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

        // CropWaterRate: one default rate per crop (IrrigationSystemId = null = all systems).
        // NOTE: SeedCropsAsync must run before this block so crops exist to iterate.
        // BUG-005: per-item idempotency via composite key (CropId, IrrigationSystemId).
        var crops = await _context.Crops.ToListAsync();
        var defaultRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Maize"]       = 550m,
            ["Wheat"]       = 450m,
            ["Sugarcane"]   = 1200m,
            ["Soybean"]     = 500m,
            ["Sunflower"]   = 480m,
            ["Groundnut"]   = 520m,
            ["Cotton"]      = 700m,
            ["Lucerne"]     = 1400m,
            ["Pasture"]     = 800m,
            ["Vegetables"]  = 600m,
            ["Citrus"]      = 900m,
            ["Grapes"]      = 700m,
            ["Stone fruit"] = 750m,
            ["Other"]       = 600m,
        };

        // Pull existing (CropId, IrrigationSystemId) pairs into memory for cheap
        // lookup. The set is small (~14 rows once seeded) so this is fine.
        var existingPairs = await _context.CropWaterRates
            .Select(r => new { r.CropId, r.IrrigationSystemId })
            .ToListAsync();
        var existingPairSet = new HashSet<(Guid CropId, Guid? IrrigationSystemId)>(
            existingPairs.Select(p => (p.CropId, p.IrrigationSystemId)));

        foreach (var crop in crops)
        {
            // Default rate row uses IrrigationSystemId = null (applies to all systems).
            if (existingPairSet.Contains((crop.CropId, (Guid?)null)))
                continue;

            if (!defaultRates.TryGetValue(crop.CropName, out var rate))
                rate = 600m;

            _context.CropWaterRates.Add(new CropWaterRate
            {
                CropWaterRateId = Guid.NewGuid(),
                CropId = crop.CropId,
                IrrigationSystemId = null,
                RatePerHaPerAnnum = rate,
                Source = "SAPWAT 4.0 SA average",
            });
        }

        await _context.SaveChangesAsync();
    }

    // ── 10. Entitlement Types ────────────────────────────────────────────
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
}
