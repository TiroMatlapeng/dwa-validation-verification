using System.Text;
using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// Focused negative coverage of the V&amp;V state machine: each control-point guard demonstrably
/// blocks advancement when its evidence is missing (surfaced in the "Cannot advance yet:" banner),
/// and the role restrictions hold (a Capturer cannot advance the workflow or issue letters; a
/// ReadOnly user cannot create a case). Each test owns a unique case so they never interfere while
/// sharing the one Kestrel app + E2E database.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class StateMachineGuardTests
{
    private readonly E2EAppFixture _fixture;
    public StateMachineGuardTests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };
    private static byte[] Pdf(string body) => Encoding.ASCII.GetBytes("%PDF-1.4\n" + body + "\n%%EOF\n");

    /// <summary>
    /// Creates an S35 case (as Validator) for a freshly-seeded in-scope property and returns its id.
    /// Used by the guard negatives, which then fast-forward to the control point under test.
    /// </summary>
    private async Task<(Guid FileMasterId, VnVTestData.Anchors Anchors)> CreateCaseAsync(IPage page, string tag)
    {
        var suffix = $"{tag}{DateTime.UtcNow:HHmmssfff}";
        var reg = $"E2E-{suffix}";
        var anchors = await VnVTestData.SeedScopedPropertyAsync(suffix);

        await page.GotoAsync("/FileMaster/Create", Idle);
        await page.FillAsync("input[name='RegistrationNumber']", reg);
        await page.FillAsync("input[name='SurveyorGeneralCode']", $"SG-{reg}");
        await page.FillAsync("input[name='PrimaryCatchment']", "X2");
        await page.FillAsync("input[name='QuaternaryCatchment']", "X21A");
        await page.FillAsync("input[name='FarmName']", "Guard Test Farm");
        await page.FillAsync("input[name='FarmNumber']", "200");
        await page.FillAsync("input[name='RegistrationDivision']", "JT");
        await page.FillAsync("input[name='FarmPortion']", "0");
        await page.SelectOptionAsync("select[name='CatchmentAreaId']", anchors.CatchmentAreaId.ToString());
        await page.SelectOptionAsync("select[name='AssessmentTrack']", "S35_Verification");
        await page.SelectOptionAsync("select[name='PropertyId']", anchors.PropertyId.ToString());
        await page.SelectOptionAsync("select[name='OrgUnitId']", anchors.OrgUnitId.ToString());
        await page.ClickAsync("input.btn-primary[type='submit'], button[type='submit'].btn-primary");
        await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));

        return (await VnVTestData.FileMasterIdByRegistrationAsync(reg), anchors);
    }

    private static async Task AssertBlockedAsync(IPage page, Guid fileMasterId, string fragment)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        var body = await page.InnerTextAsync("body");
        Assert.Contains("Cannot advance yet:", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fragment, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Attempts an advance via the UI button and confirms the case did NOT move state.</summary>
    private static async Task AssertAdvanceRejectedAsync(IPage page, Guid fileMasterId, string expectedUnchangedState)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        await page.ClickAsync("form[action*='AdvanceWorkflow'] button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Equal(expectedUnchangedState, await VnVTestData.CurrentStateNameAsync(fileMasterId));
    }

    [Fact]
    public async Task CP2_BlocksAdvance_WhenSpatialFlagAndDocumentsMissing()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP2");
            await VnVTestData.ForceStateAsync(fmId, "CP2_SpatialInfo");

            // Spatial flag missing → CP2 guard denies; documents missing → DocumentEvidenceGuard denies.
            await AssertBlockedAsync(page, fmId, "spatial information is confirmed");
            await AssertBlockedAsync(page, fmId, "must be uploaded before leaving this control point");
            await AssertAdvanceRejectedAsync(page, fmId, "CP2_SpatialInfo");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task CP5_BlocksAdvance_WhenNoMapbook()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP5");
            await VnVTestData.ForceStateAsync(fmId, "CP5_GISAnalysis");

            await AssertBlockedAsync(page, fmId, "at least one Mapbook exists");
            await AssertAdvanceRejectedAsync(page, fmId, "CP5_GISAnalysis");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task CP6_BlocksAdvance_WhenNoSapwatFieldCrop()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP6");
            await VnVTestData.ForceStateAsync(fmId, "CP6_FieldCropSAPWAT");

            await AssertBlockedAsync(page, fmId, "SAPWAT calculation result");
            await AssertAdvanceRejectedAsync(page, fmId, "CP6_FieldCropSAPWAT");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task CP7_BlocksAdvance_WhenNoEntitlement()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP7");
            await VnVTestData.ForceStateAsync(fmId, "CP7_ELUCalculated");

            await AssertBlockedAsync(page, fmId, "Entitlement (ELU volume) is linked");
            await AssertAdvanceRejectedAsync(page, fmId, "CP7_ELUCalculated");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task CP8_BlocksAdvance_WhenNoDamAndNotMarkedNA()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP8");
            await VnVTestData.ForceStateAsync(fmId, "CP8_DamVolumes");

            await AssertBlockedAsync(page, fmId, "DamCalculation exists or the case is marked Dam N/A");
            await AssertAdvanceRejectedAsync(page, fmId, "CP8_DamVolumes");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task CP9_BlocksAdvance_WhenNoForestationAndNotMarkedNA()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "CP9");
            await VnVTestData.ForceStateAsync(fmId, "CP9_SFRACalculated");

            await AssertBlockedAsync(page, fmId, "Forestation record exists or the case is marked SFRA N/A");
            await AssertAdvanceRejectedAsync(page, fmId, "CP9_SFRACalculated");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task Capturer_CannotAdvanceWorkflow()
    {
        // A Validator creates the case; a Capturer (CanCapture but NOT CanTransitionWorkflow)
        // then attempts to advance it.
        var validatorPage = await _fixture.NewPageAsync();
        Guid fmId;
        try
        {
            await _fixture.LoginAsync(validatorPage, DemoUsers.Validator("3"));
            (fmId, _) = await CreateCaseAsync(validatorPage, "RBACadv");
        }
        finally { await E2EAppFixture.DisposePageAsync(validatorPage); }

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Capturer("3"));
            await page.GotoAsync($"/FileMaster/Details/{fmId}", Idle);

            // The Capturer never sees the Advance button (CanTransitionWorkflow gate in the panel).
            Assert.Equal(0, await page.Locator("form[action*='AdvanceWorkflow'] button[type='submit']").CountAsync());

            // And a direct POST to AdvanceWorkflow is forbidden by the policy. Send a VALID
            // antiforgery token (from the Capturer's own Details page) so the request gets past
            // the antiforgery filter and the CanTransitionWorkflow policy is what refuses it —
            // otherwise this test would only prove antiforgery, not RBAC. The cookie pipeline
            // answers a denied policy with a 302 to AccessDenied, which the API client follows.
            var afToken = await page.GetAttributeAsync("input[name='__RequestVerificationToken']", "value");
            Assert.False(string.IsNullOrEmpty(afToken), "No antiforgery token found on Capturer's Details page.");
            var resp = await page.Context.APIRequest.PostAsync(
                $"{_fixture.BaseUrl}/FileMaster/AdvanceWorkflow/{fmId}",
                new APIRequestContextOptions
                {
                    Form = page.Context.APIRequest.CreateFormData()
                        .Set("__RequestVerificationToken", afToken!)
                        .Set("notes", "x"),
                });
            Assert.True(resp.Status is 403
                    || resp.Url.Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase),
                $"Expected policy denial for Capturer advance, got {resp.Status} at {resp.Url}.");

            // State is unchanged regardless.
            Assert.Equal("CP1_WARMSObtained", await VnVTestData.CurrentStateNameAsync(fmId));
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task ReadOnly_CannotCreateCase()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);
            await page.GotoAsync("/FileMaster/Create", Idle);

            // CanCreateCase is denied → cookie pipeline routes to access-denied, the Create form
            // never renders.
            var deniedUrl = page.Url.Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
            var hasForm = await page.Locator("input[name='RegistrationNumber']").CountAsync() > 0;
            Assert.True(deniedUrl || !hasForm,
                $"ReadOnly must not reach the Create form. URL: {page.Url}");
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task Letter1_CannotBeIssued_BeforeReachingLetterPhase()
    {
        // CanIssueLetterAsync denies S35_L1 from an early state. Drive the rejected POST directly:
        // the Letters panel isn't even rendered before the letter phase, so a crafted POST is the
        // only way to attempt it — and the controller must refuse with no state change.
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "LtrEarly");
            await VnVTestData.ForceStateAsync(fmId, "CP4_AdditionalInfo");

            // Fetch a valid antiforgery token from the Details page so the POST passes the AF filter
            // and actually reaches the controller's CanIssueLetterAsync gate.
            await page.GotoAsync($"/FileMaster/Details/{fmId}", Idle);
            var token = await page.GetAttributeAsync("input[name='__RequestVerificationToken']", "value");
            Assert.False(string.IsNullOrEmpty(token), "No antiforgery token found on Details page.");

            var resp = await page.Context.APIRequest.PostAsync(
                $"{_fixture.BaseUrl}/FileMaster/IssueLetter/{fmId}",
                new APIRequestContextOptions
                {
                    Form = page.Context.APIRequest.CreateFormData()
                        .Set("__RequestVerificationToken", token!)
                        .Set("letterAction", "IssueLetter1")
                        .Set("recipient", "Mr A. Landowner")
                        .Set("deliveryMethod", "InPerson")
                        .Set("issuedDate", DateTime.Today.ToString("yyyy-MM-dd")),
                });

            // The controller redirects back to Details with a TempData error (no exception, no move).
            Assert.True(resp.Status is 200 or 302, $"Unexpected status {resp.Status} for early letter issue.");
            Assert.Equal("CP4_AdditionalInfo", await VnVTestData.CurrentStateNameAsync(fmId));
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task Advance_CannotEnterLetterState_WithoutIssuingLetter()
    {
        // The UI hides the Advance button once a case is letter-ready, but the engine itself must
        // refuse a crafted advance from CP_StakeholderWorkshop into S35_Letter1Issued — otherwise
        // the case lands in a letter state with no LetterIssuance and wedges permanently
        // (LetterServiceConfirmedGuard has no issuance to ever confirm service on).
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            var (fmId, _) = await CreateCaseAsync(page, "LtrAdv");
            await VnVTestData.ForceStateAsync(fmId, "CP_StakeholderWorkshop");

            await page.GotoAsync($"/FileMaster/Details/{fmId}", Idle);
            var token = await page.GetAttributeAsync("input[name='__RequestVerificationToken']", "value");
            Assert.False(string.IsNullOrEmpty(token), "No antiforgery token found on Details page.");

            var resp = await page.Context.APIRequest.PostAsync(
                $"{_fixture.BaseUrl}/FileMaster/AdvanceWorkflow/{fmId}",
                new APIRequestContextOptions
                {
                    Form = page.Context.APIRequest.CreateFormData()
                        .Set("__RequestVerificationToken", token!),
                });

            // Engine refusal surfaces as a TempData error on the post-redirect page (this response).
            Assert.True(resp.Status is 200 or 302, $"Unexpected status {resp.Status} for blocked letter-state advance.");
            Assert.Contains("Direct workflow advance into letter state",
                await resp.TextAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("CP_StakeholderWorkshop", await VnVTestData.CurrentStateNameAsync(fmId));
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }
}
