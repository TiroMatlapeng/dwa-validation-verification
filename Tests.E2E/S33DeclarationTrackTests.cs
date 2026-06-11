using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// End-to-end coverage of the Section 33(2) Kader Asmal declaration track: a case on this track
/// skips CP5–CP9, CP11 and the review/workshop control points, jumping from CP4 straight to the
/// non-terminal holding state <c>S33_2_ReadyForDeclaration</c>. From there a direct Advance is
/// refused (the documented message), the S33(2) declaration letter cannot be issued until both
/// rates-paid is confirmed AND an ELU entitlement is linked, and a successful issuance transitions
/// the case to <c>S33_2_DeclarationIssued</c> — from which the case can be closed.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class S33DeclarationTrackTests
{
    private readonly E2EAppFixture _fixture;
    public S33DeclarationTrackTests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };

    [Fact]
    public async Task S33_2_Track_SkipsToDeclaration_GuardsIssuance_AndCloses()
    {
        var suffix = $"S332{DateTime.UtcNow:HHmmssfff}";
        var reg = $"E2E-S332-{suffix}";
        var anchors = await VnVTestData.SeedScopedPropertyAsync(suffix);

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));

            // ── Create the case on the S33(2) declaration track ──
            await page.GotoAsync("/FileMaster/Create", Idle);
            await page.FillAsync("input[name='RegistrationNumber']", reg);
            await page.FillAsync("input[name='SurveyorGeneralCode']", $"SG-{reg}");
            await page.FillAsync("input[name='PrimaryCatchment']", "X2");
            await page.FillAsync("input[name='QuaternaryCatchment']", "X21A");
            await page.FillAsync("input[name='FarmName']", "Kader Asmal Test Farm");
            await page.FillAsync("input[name='FarmNumber']", "300");
            await page.FillAsync("input[name='RegistrationDivision']", "JT");
            await page.FillAsync("input[name='FarmPortion']", "0");
            await page.SelectOptionAsync("select[name='CatchmentAreaId']", anchors.CatchmentAreaId.ToString());
            await page.SelectOptionAsync("select[name='AssessmentTrack']", "S33_2_Declaration");
            await page.SelectOptionAsync("select[name='PropertyId']", anchors.PropertyId.ToString());
            await page.SelectOptionAsync("select[name='OrgUnitId']", anchors.OrgUnitId.ToString());
            await page.ClickAsync("input.btn-primary[type='submit'], button[type='submit'].btn-primary");
            await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));

            var fmId = await VnVTestData.FileMasterIdByRegistrationAsync(reg);

            // ── Advance through CP1 (ungated) → CP2 → CP3 → CP4 capturing the CP evidence flags ──
            await AdvanceUntilStateAsync(page, fmId, "CP2_SpatialInfo");
            await RecordEvidenceAsync(page, fmId, "SpatialInfoConfirmed");

            // CP2 still needs the Title Deed + SG diagram documents to leave (DocumentEvidenceGuard).
            await UploadDocumentAsync(page, fmId, "TitleDeedReport", "title-deed.pdf");
            await UploadDocumentAsync(page, fmId, "SGDiagram", "sg-diagram.pdf");
            await AdvanceAsync(page, fmId);
            await AssertStateAsync(fmId, "CP3_WARMSEvaluation");

            await RecordEvidenceAsync(page, fmId, "WarmsReviewed");
            await UploadDocumentAsync(page, fmId, "WARMSReport", "warms-report.pdf");
            await AdvanceAsync(page, fmId);
            await AssertStateAsync(fmId, "CP4_AdditionalInfo");

            await RecordEvidenceAsync(page, fmId, "AdditionalInfoReviewed");

            // ── Advancing from CP4 on the S33(2) track skips CP5–CP9/CP11/review/workshop ──
            await AdvanceAsync(page, fmId);
            await AssertStateAsync(fmId, "S33_2_ReadyForDeclaration");

            // The phase tracker hides the skipped control points; history shows the jump CP4 → ready.
            await page.GotoAsync($"/FileMaster/Details/{fmId}", Idle);
            var historyStates = await page.Locator(".timeline .timeline-state").AllInnerTextsAsync();
            Assert.Contains("S33_2_ReadyForDeclaration", historyStates);
            Assert.DoesNotContain("CP5_GISAnalysis", historyStates);
            Assert.DoesNotContain("CP_StakeholderWorkshop", historyStates);

            // ── A direct Advance from the holding state is refused with the documented message ──
            // (No Advance button renders — it's letter-phase — so POST directly to prove the engine
            //  rejects it. The case must not move.)
            var token = await page.GetAttributeAsync("input[name='__RequestVerificationToken']", "value");
            Assert.False(string.IsNullOrEmpty(token), "No antiforgery token on Details page.");
            var advResp = await page.Context.APIRequest.PostAsync(
                $"{_fixture.BaseUrl}/FileMaster/AdvanceWorkflow/{fmId}",
                new APIRequestContextOptions
                {
                    Form = page.Context.APIRequest.CreateFormData().Set("__RequestVerificationToken", token!),
                });
            Assert.True(advResp.Status is 200 or 302, $"Unexpected status {advResp.Status} for blocked advance.");
            Assert.Equal("S33_2_ReadyForDeclaration", await VnVTestData.CurrentStateNameAsync(fmId));
            // The controller surfaces the engine's refusal via TempData; the API client follows
            // the redirect, so the rendered (TempData-consuming) page IS this response body.
            var afterBody = await advResp.TextAsync();
            Assert.Contains("Direct workflow advance is not permitted from S33_2_ReadyForDeclaration",
                afterBody, StringComparison.OrdinalIgnoreCase);

            // ── Letter issuance requires RegionalManager+ (CanIssueLetter policy) — the
            //    Validator sees no issue forms, mirroring the S35 journey's user switch. ──
            var regionalPage = await _fixture.NewPageAsync();
            try
            {
                await _fixture.LoginAsync(regionalPage, DemoUsers.Regional("3"));

                // ── Issuing the S33(2) declaration is blocked until rates-paid + entitlement are set ──
                // The controller's refusal is a TempData error rendered on the post-redirect page,
                // which IssueLetterAsync lands on — assert there, before any further navigation
                // consumes the TempData.
                await IssueLetterAsync(regionalPage, fmId, "IssueS33_2", "RegisteredPost", "Inkomati Irrigation Board");
                Assert.Equal("S33_2_ReadyForDeclaration", await VnVTestData.CurrentStateNameAsync(fmId));
                Assert.Contains("rates paid up to", await regionalPage.InnerTextAsync("body"), StringComparison.OrdinalIgnoreCase);

                // Confirm rates paid, but entitlement still missing → still blocked.
                await VnVTestData.ConfirmRatesPaidAsync(fmId);
                await IssueLetterAsync(regionalPage, fmId, "IssueS33_2", "RegisteredPost", "Inkomati Irrigation Board");
                Assert.Equal("S33_2_ReadyForDeclaration", await VnVTestData.CurrentStateNameAsync(fmId));
                Assert.Contains("until an ELU entitlement", await regionalPage.InnerTextAsync("body"), StringComparison.OrdinalIgnoreCase);

                // Link the ELU entitlement → issuance now succeeds and transitions to S33_2_DeclarationIssued.
                await VnVTestData.SeedAndLinkEntitlementAsync(fmId, 42000m);
                await IssueLetterAsync(regionalPage, fmId, "IssueS33_2", "RegisteredPost", "Inkomati Irrigation Board");
                await AssertStateAsync(fmId, "S33_2_DeclarationIssued");

                // ── Close the case → terminal ──
                await regionalPage.GotoAsync($"/FileMaster/Details/{fmId}", Idle);
                await MarkLetterResponseAsync(regionalPage, fmId, "CloseCase");
                await AssertStateAsync(fmId, "Closed");

                await regionalPage.GotoAsync($"/FileMaster/Details/{fmId}", Idle);
                Assert.Equal(1, await regionalPage.Locator(".current-state-line .badge-green").CountAsync());
            }
            finally { await E2EAppFixture.DisposePageAsync(regionalPage); }
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    // ── UI helpers (mirror the S35 journey helpers) ───────────────────────────────

    private async Task AdvanceAsync(IPage page, Guid fileMasterId)
    {
        await page.GotoAsync($"/FileMaster/Details/{fileMasterId}", Idle);
        await page.ClickAsync("form[action*='AdvanceWorkflow'] button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

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

    private async Task UploadDocumentAsync(IPage page, Guid fileMasterId, string documentType, string fileName)
    {
        await page.GotoAsync($"/Document/Upload?fileMasterId={fileMasterId}", Idle);
        await page.SelectOptionAsync("select[name='DocumentType']", documentType);
        await page.SetInputFilesAsync("input[name='File']", new FilePayload
        {
            Name = fileName,
            MimeType = "application/pdf",
            Buffer = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n" + documentType + "\n%%EOF\n"),
        });
        await page.ClickAsync("#upload-submit");
        await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));
    }

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

    private static async Task AssertStateAsync(Guid fileMasterId, string expected) =>
        Assert.Equal(expected, await VnVTestData.CurrentStateNameAsync(fileMasterId));
}
