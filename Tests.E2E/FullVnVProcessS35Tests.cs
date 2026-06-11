using System.Text;
using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// End-to-end browser drive of a COMPLETE Section 35 verification V&amp;V case, from creation through
/// every control point to a terminal Closed state — exercising the real WorkflowEngine, every
/// transition guard, the letter sub-state machine, and the PAJA gate against the live Kestrel app
/// and the isolated <c>dwa_val_ver_e2e</c> database.
///
/// The journey (mirrors the seeded WorkflowState order and AvailableLetterActions switch):
///   CP1 sub-steps (ungated) → CP2 (spatial flag + Title Deed + SG diagram docs)
///   → CP3 (WARMS flag + WARMS report doc) → CP4 (additional-info flag) → CP5 (Mapbooks)
///   → CP6 (Field &amp; Crop + SAPWAT) → CP7 (Entitlement) → CP8/CP9 (marked N/A)
///   → CP11 (file compilation re-check) → CP_PrePublicReview (Regional Manager approves)
///   → CP_StakeholderWorkshop (date + attendance) → IssueLetter1 (InPerson)
///   → confirm service → MarkLetter1Responded → PAJA checklist → IssueLetter3
///   → MarkELUConfirmed → CloseCase → Closed (terminal).
///
/// Evidence that has a UI route is captured through the browser; evidence with no UI route
/// (Mapbooks, Entitlement link, Authorisation, letter ServiceConfirmedDate, the Dam/SFRA N/A flags)
/// is seeded directly via <see cref="VnVTestData"/>, exactly as a back-office import would.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class FullVnVProcessS35Tests
{
    private readonly E2EAppFixture _fixture;
    public FullVnVProcessS35Tests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };

    // Minimal valid PDF: %PDF magic bytes (passes FileSignatureValidator) + harmless body
    // (EicarVirusScanner marks it Clean).
    private static byte[] Pdf(string body) => Encoding.ASCII.GetBytes("%PDF-1.4\n" + body + "\n%%EOF\n");

    [Fact]
    public async Task S35_Verification_FullJourney_ReachesClosedTerminalState()
    {
        var suffix = $"S35{DateTime.UtcNow:HHmmssfff}";
        var reg = $"E2E-S35-{suffix}";
        var anchors = await VnVTestData.SeedScopedPropertyAsync(suffix);

        var page = await _fixture.NewPageAsync();
        try
        {
            // ── Create the case as a Validator (CanCreateCase) ──────────────────────
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            await CreateCaseAsync(page, reg, anchors, "S35_Verification");

            var fileMasterId = await VnVTestData.FileMasterIdByRegistrationAsync(reg);
            var detailsUrl = $"/FileMaster/Details/{fileMasterId}";

            // ── Phase 1: CP1 sub-steps 1.1–1.7 are ungated — advance straight through to CP2 ──
            await AdvanceUntilStateAsync(page, fileMasterId, "CP2_SpatialInfo");

            // ── CP2: blocked until spatial flag + Title Deed report + SG diagram are present ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "spatial information is confirmed");

            await RecordEvidenceAsync(page, fileMasterId, "SpatialInfoConfirmed");
            await UploadDocumentAsync(page, fileMasterId, "TitleDeedReport", "title-deed.pdf");
            await UploadDocumentAsync(page, fileMasterId, "SGDiagram", "sg-diagram.pdf");
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP3_WARMSEvaluation");

            // ── CP3: blocked until WARMS flag + WARMS report doc ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "WARMS evaluation is recorded");
            await RecordEvidenceAsync(page, fileMasterId, "WarmsReviewed");
            await UploadDocumentAsync(page, fileMasterId, "WARMSReport", "warms-report.pdf");
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP4_AdditionalInfo");

            // ── CP4: blocked until additional-info flag ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "additional information review is recorded");
            await RecordEvidenceAsync(page, fileMasterId, "AdditionalInfoReviewed");
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP5_GISAnalysis");

            // ── CP5: blocked until a Mapbook exists (no UI route → seed both periods) ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "at least one Mapbook exists");
            await VnVTestData.SeedMapbookAsync(fileMasterId, "Qualifying");
            await VnVTestData.SeedMapbookAsync(fileMasterId, "Current");
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP6_FieldCropSAPWAT");

            // ── CP6: blocked until a Field & Crop record with SAPWAT > 0 (via the real UI) ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "SAPWAT calculation result");
            await AddFieldAndCropWithSapwatAsync(page, anchors.PropertyId);
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP7_ELUCalculated");

            // ── CP7: blocked until an Entitlement is linked (no UI route → seed + link) ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "Entitlement (ELU volume) is linked");
            await VnVTestData.SeedAndLinkEntitlementAsync(fileMasterId, 75000m);
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP8_DamVolumes");

            // ── CP8 + CP9: mark Dam and SFRA N/A (no UI toggle), then advance through both ──
            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "DamCalculation exists or the case is marked Dam N/A");
            await VnVTestData.MarkDamAndSfraNAAsync(fileMasterId);
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP9_SFRACalculated");

            // CP9 is a letter-ready state, so advance-blocking reasons are suppressed there.
            // It still needs Authorisation seeded for the CP11 compilation re-check downstream.
            await VnVTestData.SeedAuthorisationAsync(fileMasterId);
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP11_FileCompiled");

            // ── CP11: full Appendix-A re-check passes (all evidence present) → CP_PrePublicReview ──
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP_PrePublicReview");

            // ── CP_PrePublicReview: only Regional Manager+ may approve. Switch user. ──
            await E2EAppFixture.DisposePageAsync(page);
            page = await _fixture.NewPageAsync();
            await _fixture.LoginAsync(page, DemoUsers.Regional("3"));

            await page.GotoAsync(detailsUrl, Idle);
            await AssertBlockingContainsAsync(page, "Pre-public review approval has not been recorded");
            await RecordEvidenceAsync(page, fileMasterId, "PrePublicReviewApproved");
            await AdvanceAsync(page, fileMasterId);
            await AssertCurrentStateAsync(fileMasterId, "CP_StakeholderWorkshop");

            // ── CP_StakeholderWorkshop: record date + attendance. This is itself a letter-ready
            //    state — the next action is "Issue Letter 1", not an Advance. ──
            await page.GotoAsync(detailsUrl, Idle);
            await RecordStakeholderWorkshopAsync(page, fileMasterId, attendance: 25);
            await AssertCurrentStateAsync(fileMasterId, "CP_StakeholderWorkshop");

            // Letters phase is for Regional Manager+ (CanIssueLetter); the Regional user is signed in.
            // ── Issue Letter 1 (S35(1) notice) served in person per S35(2)(d) ──
            await IssueLetterAsync(page, fileMasterId, "IssueLetter1", "InPerson", "Mr A. Landowner");
            await AssertCurrentStateAsync(fileMasterId, "S35_Letter1Issued");

            // ── Proof of service must be recorded before the case can leave S35_Letter1Issued ──
            await page.GotoAsync(detailsUrl, Idle);
            await MarkLetterResponseAsync(page, fileMasterId, "MarkLetter1Responded");
            // Without ServiceConfirmedDate the guard denies the transition — state is unchanged.
            await AssertCurrentStateAsync(fileMasterId, "S35_Letter1Issued");

            await VnVTestData.ConfirmLetterServiceAsync(fileMasterId, "S35_L1");
            await MarkLetterResponseAsync(page, fileMasterId, "MarkLetter1Responded");
            await AssertCurrentStateAsync(fileMasterId, "S35_Letter1Responded");

            // ── Letter 3 (ELU certificate) cannot be issued until the PAJA checklist is complete ──
            await page.GotoAsync(detailsUrl, Idle);
            await IssueLetterAsync(page, fileMasterId, "IssueLetter3", "RegisteredPost", "Mr A. Landowner");
            await AssertCurrentStateAsync(fileMasterId, "S35_Letter1Responded"); // blocked by PAJA guard

            await CompletePajaChecklistAsync(page, fileMasterId);
            await IssueLetterAsync(page, fileMasterId, "IssueLetter3", "RegisteredPost", "Mr A. Landowner");
            await AssertCurrentStateAsync(fileMasterId, "S35_Letter3Issued");

            // ── Confirm the ELU determination, then close the case ──
            await page.GotoAsync(detailsUrl, Idle);
            await MarkLetterResponseAsync(page, fileMasterId, "MarkELUConfirmed");
            await AssertCurrentStateAsync(fileMasterId, "S35_ELUConfirmed");

            await page.GotoAsync(detailsUrl, Idle);
            await MarkLetterResponseAsync(page, fileMasterId, "CloseCase");
            await AssertCurrentStateAsync(fileMasterId, "Closed");

            // ── Terminal state is shown in the UI and the full transition history is recorded ──
            await page.GotoAsync(detailsUrl, Idle);
            var closedBadge = page.Locator(".current-state-line .badge-green");
            Assert.Equal(1, await closedBadge.CountAsync());
            var stateLine = await page.InnerTextAsync(".current-state-line");
            Assert.Contains("Closed", stateLine, StringComparison.OrdinalIgnoreCase);

            // The history timeline lists every key transition the journey passed through.
            var historyStates = await page.Locator(".timeline .timeline-state").AllInnerTextsAsync();
            foreach (var expected in new[]
                     {
                         "CP2_SpatialInfo", "CP5_GISAnalysis", "CP7_ELUCalculated", "CP11_FileCompiled",
                         "CP_PrePublicReview", "CP_StakeholderWorkshop", "S35_Letter1Issued",
                         "S35_Letter1Responded", "S35_Letter3Issued", "S35_ELUConfirmed", "Closed",
                     })
            {
                Assert.Contains(expected, historyStates);
            }
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    // ── Letter-phase UI helpers ───────────────────────────────────────────────────

    private async Task IssueLetterAsync(IPage page, Guid fileMasterId, string letterAction, string deliveryMethod, string recipient)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        var form = page.Locator($"form:has(input[name='letterAction'][value='{letterAction}'])");
        await form.Locator("input[name='recipient']").FillAsync(recipient);
        await form.Locator("select[name='deliveryMethod']").SelectOptionAsync(deliveryMethod);
        await form.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task MarkLetterResponseAsync(IPage page, Guid fileMasterId, string letterAction)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        var form = page.Locator($"form:has(input[name='letterAction'][value='{letterAction}'])");
        await form.Locator("button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CompletePajaChecklistAsync(IPage page, Guid fileMasterId)
    {
        await page.GotoAsync($"/FileMaster/PAJAChecklist/{fileMasterId}", Idle);
        await page.FillAsync("textarea[name='FactualBasis']", "Use existed throughout the qualifying period per GIS and field survey.");
        await page.FillAsync("textarea[name='LegalBasis']", "Riparian abstraction lawful under S21(a); within S9B limits.");
        await page.FillAsync("textarea[name='UserInputConsideration']", "Owner representations considered; no objection raised.");
        await page.FillAsync("textarea[name='FinalReasoning']", "ELU confirmed at the assessed lawful volume.");
        await page.ClickAsync("button[type='submit'].btn-primary");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // ── UI step helpers ─────────────────────────────────────────────────────────

    private async Task CreateCaseAsync(IPage page, string registrationNumber, VnVTestData.Anchors anchors, string track)
    {
        await page.GotoAsync("/FileMaster/Create", Idle);
        await page.FillAsync("input[name='RegistrationNumber']", registrationNumber);
        await page.FillAsync("input[name='SurveyorGeneralCode']", $"SG-{registrationNumber}");
        await page.FillAsync("input[name='PrimaryCatchment']", "X2");
        await page.FillAsync("input[name='QuaternaryCatchment']", "X21A");
        await page.FillAsync("input[name='FarmName']", "E2E Test Farm");
        await page.FillAsync("input[name='FarmNumber']", "100");
        await page.FillAsync("input[name='RegistrationDivision']", "JT");
        await page.FillAsync("input[name='FarmPortion']", "0");
        await page.SelectOptionAsync("select[name='CatchmentAreaId']", anchors.CatchmentAreaId.ToString());
        await page.SelectOptionAsync("select[name='AssessmentTrack']", track);
        await page.SelectOptionAsync("select[name='PropertyId']", anchors.PropertyId.ToString());
        await page.SelectOptionAsync("select[name='OrgUnitId']", anchors.OrgUnitId.ToString());

        await page.ClickAsync("input.btn-primary[type='submit'], button[type='submit'].btn-primary");
        await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Clicks "Advance to Next CP" on the workflow panel and waits for the Details reload.</summary>
    private async Task AdvanceAsync(IPage page, Guid fileMasterId)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        await page.ClickAsync("form[action*='AdvanceWorkflow'] button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Advances repeatedly until the case reaches <paramref name="targetState"/> (CP1 chain).</summary>
    private async Task AdvanceUntilStateAsync(IPage page, Guid fileMasterId, string targetState)
    {
        for (var i = 0; i < 12; i++)
        {
            if (await VnVTestData.CurrentStateNameAsync(fileMasterId) == targetState) return;
            await AdvanceAsync(page, fileMasterId);
        }
        Assert.Equal(targetState, await VnVTestData.CurrentStateNameAsync(fileMasterId));
    }

    private async Task RecordEvidenceAsync(IPage page, Guid fileMasterId, string hiddenFlagName)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        await page.ClickAsync($"form[action*='RecordCpEvidence']:has(input[name='{hiddenFlagName}']) button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task RecordStakeholderWorkshopAsync(IPage page, Guid fileMasterId, int attendance)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        await page.FillAsync("input[name='StakeholderWorkshopDate']", DateTime.Today.ToString("yyyy-MM-dd"));
        await page.FillAsync("input[name='StakeholderWorkshopVenue']", "Mbombela Civic Hall");
        await page.FillAsync("input[name='StakeholderWorkshopAttendance']", attendance.ToString());
        await page.ClickAsync("form[action*='RecordCpEvidence']:has(input[name='StakeholderWorkshopDate']) button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task UploadDocumentAsync(IPage page, Guid fileMasterId, string documentType, string fileName)
    {
        await page.GotoAsync($"/Document/Upload?fileMasterId={fileMasterId}", Idle);
        await page.SelectOptionAsync("select[name='DocumentType']", documentType);
        await page.SetInputFilesAsync("input[name='File']", new FilePayload
        {
            Name = fileName,
            MimeType = "application/pdf",
            Buffer = Pdf($"{documentType} evidence"),
        });
        await page.ClickAsync("#upload-submit");
        await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds a Field &amp; Crop record with SAPWAT &gt; 0 through the real UI form.</summary>
    private async Task AddFieldAndCropWithSapwatAsync(IPage page, Guid propertyId)
    {
        await page.GotoAsync($"/FieldAndCrop/Create?propertyId={propertyId}", Idle);
        await page.FillAsync("input[name='FieldNumber']", "F01");
        await page.FillAsync("input[name='FieldArea']", "10.5");
        await SelectFirstRealOptionAsync(page, "select[name='PeriodId']");
        await SelectFirstRealOptionAsync(page, "select[name='CropId']");
        await page.FillAsync("input[name='CropArea']", "8.0");
        await SelectFirstRealOptionAsync(page, "select[name='WaterSourceId']");
        await page.FillAsync("input[name='RotationFactor']", "1");
        await page.FillAsync("input[name='SAPWATCalculationResult']", "6500");
        await page.ClickAsync("form[action*='FieldAndCrop/Create'] button[type='submit'], button.btn-primary[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Selects the first non-empty &lt;option&gt; value of a select (skips the placeholder).</summary>
    private static async Task SelectFirstRealOptionAsync(IPage page, string selector)
    {
        var value = await page.EvalOnSelectorAsync<string>(selector,
            "el => Array.from(el.options).map(o => o.value).find(v => v && v.length > 0) || ''");
        Assert.False(string.IsNullOrEmpty(value), $"No selectable option in {selector}");
        await page.SelectOptionAsync(selector, value);
    }

    // ── Assertion helpers ───────────────────────────────────────────────────────

    private static async Task AssertCurrentStateAsync(Guid fileMasterId, string expected)
    {
        var actual = await VnVTestData.CurrentStateNameAsync(fileMasterId);
        Assert.Equal(expected, actual);
    }

    /// <summary>Asserts the amber "Cannot advance yet:" banner is shown and contains the given fragment.</summary>
    private static async Task AssertBlockingContainsAsync(IPage page, string fragment)
    {
        var body = await page.InnerTextAsync("body");
        Assert.Contains("Cannot advance yet:", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fragment, body, StringComparison.OrdinalIgnoreCase);
    }
}
