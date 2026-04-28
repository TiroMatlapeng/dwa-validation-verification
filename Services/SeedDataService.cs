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
        await SeedProvincesAsync();
        await SeedWaterManagementAreasAsync();
        await SeedWorkflowStatesAsync();
        await SeedLetterTypesAsync();
        await SeedAuthorisationTypesAsync();
        await SeedPeriodsAsync();
        await SeedGwcaProclamationRulesAsync();
        await SeedSampleCasesAsync();
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
        if (await _context.WorkflowStates.AnyAsync())
            return;

        var states = new List<WorkflowState>
        {
            // Phase: Inception (CP1 sub-steps)
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_WARMSObtained",       Phase = "Inception",      DisplayOrder = 1,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_SatelliteImagery",    Phase = "Inception",      DisplayOrder = 2,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_DatabaseAudit",       Phase = "Inception",      DisplayOrder = 3,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_UnregisteredUsers",   Phase = "Inception",      DisplayOrder = 4,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_DatabaseAnalysis",    Phase = "Inception",      DisplayOrder = 5,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_InceptionReport",     Phase = "Inception",      DisplayOrder = 6,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_PublicParticipation", Phase = "Inception",      DisplayOrder = 7,  IsTerminal = false },

            // Phase: Validation — Technical: did use EXIST, what EXTENT (CP2–CP6)
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP2_SpatialInfo",         Phase = "Validation",     DisplayOrder = 8,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP3_WARMSEvaluation",     Phase = "Validation",     DisplayOrder = 9,  IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_AdditionalInfo",      Phase = "Validation",     DisplayOrder = 10, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_GISAnalysis",         Phase = "Validation",     DisplayOrder = 11, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP6_FieldCropSAPWAT",     Phase = "Validation",     DisplayOrder = 12, IsTerminal = false },

            // Phase: Verification — Legal: was use LAWFUL (CP7–CP9 + letter sub-states)
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP7_ELUCalculated",       Phase = "Verification",   DisplayOrder = 13, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP8_DamVolumes",          Phase = "Verification",   DisplayOrder = 14, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP9_SFRACalculated",      Phase = "Verification",   DisplayOrder = 15, IsTerminal = false },

            // Phase: Verification — Section 35 letter sub-states (Track A)
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter1Issued",           Phase = "Verification", DisplayOrder = 16, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter1Responded",        Phase = "Verification", DisplayOrder = 17, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter1ARequired",        Phase = "Verification", DisplayOrder = 18, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter1AIssued",          Phase = "Verification", DisplayOrder = 19, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter1AResponded",       Phase = "Verification", DisplayOrder = 20, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_AdditionalInfoRequired",  Phase = "Verification", DisplayOrder = 21, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter2Issued",           Phase = "Verification", DisplayOrder = 22, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter2Responded",        Phase = "Verification", DisplayOrder = 23, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter2ARequired",        Phase = "Verification", DisplayOrder = 24, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter2AIssued",          Phase = "Verification", DisplayOrder = 25, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter3Issued",           Phase = "Verification", DisplayOrder = 26, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_ELUConfirmed",            Phase = "Verification", DisplayOrder = 27, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_UnlawfulUseFound",        Phase = "Verification", DisplayOrder = 28, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter4AIssued",          Phase = "Verification", DisplayOrder = 29, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter4And5Issued",       Phase = "Verification", DisplayOrder = 30, IsTerminal = false },

            // Phase: Verification — Section 33 declaration sub-states (Tracks B & C)
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_DeclarationIssued",     Phase = "Verification", DisplayOrder = 31, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_3_DeclarationIssued",     Phase = "Verification", DisplayOrder = 32, IsTerminal = false },

            // Terminal states
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "Closed",                     Phase = "Verification", DisplayOrder = 33, IsTerminal = true  },
        };

        _context.WorkflowStates.AddRange(states);
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
        if (await _context.GwcaProclamationRules.AnyAsync())
            return;

        // Only seed if we have a GWCA to attach rules to
        var blydeRiver = await _context.GovernmentWaterControlAreas
            .FirstOrDefaultAsync(g => g.GovernmentWaterControlAreaName.Contains("Blyde"));

        if (blydeRiver == null)
            return;

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

        _context.GwcaProclamationRules.AddRange(rules);
        await _context.SaveChangesAsync();
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
}
