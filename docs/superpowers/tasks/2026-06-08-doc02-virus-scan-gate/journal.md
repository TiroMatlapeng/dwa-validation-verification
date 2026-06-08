# Task: DOC-02 — virus-scan gate (Slice 5)

**Start:** 2026-06-08
**Branch:** fix/remediation-wave4 (main working tree)
**Plan:** docs/2026-06-08-build-status-report.html → Tier 1 step 3. 3 June finding: "Uploads are never virus-scanned yet downloadable. Download only blocks 'Infected' (never set); should require 'Clean' + magic-byte validation." (Magic-byte already done — DOC-03/a38901e. This slice = scanning + the Clean gate.)

**Goal:** Uploads are actually scanned; a download is served ONLY when its scan status is "Clean" (fail-closed). Provable end-to-end in a browser (clean file → downloadable; EICAR test file → blocked).

## Ground truth
- `Models/Document.cs`: `VirusScanStatus` (string? — "Pending"/"Clean"/"Infected").
- `Controllers/DocumentController.cs`: Upload (`[Authorize(CanCapture)]`) sets `VirusScanStatus="Pending"` (:108) after magic-byte validation (`FileSignatureValidator.MatchesExtension`); Download (:128) blocks ONLY "Infected" (:136) with an explicit DOC-02 TODO.
- `Areas/ExternalPortal/Controllers/DocumentController.cs`: Upload sets "Pending" (:129). (Check whether it exposes a document Download; gate it too if so.)
- No `IVirusScanner` exists. `Services/Documents/` has only DocumentTypes + FileSignatureValidator.
- `Services/Workflow/Guards/DocumentEvidenceGuard.cs` + `FlagGuards.cs:232` treat null/Pending/Clean as acceptable evidence, block only "Infected".

## Design
1. **Abstraction:** `Services/Documents/IVirusScanner.cs` — `Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct)`; `VirusScanResult` enum/record { Clean, Infected, Error }.
2. **Default impl:** `EicarVirusScanner` — flags content containing the standard EICAR antivirus test signature as Infected, else Clean. Deterministic, no infra, and a legitimate test scanner. Register in DI. Leave a clear config seam for a real ClamAV/Defender scanner later (note in code; not built this slice — no clamd to test against).
3. **Upload (internal + portal):** after magic-byte validation, scan the content. Infected → REJECT (do not persist the blob or the Document row; return a clear validation error). Clean → persist with `VirusScanStatus="Clean"`. Scanner Error → fail-closed (reject, or persist non-Clean and block download). No more hard "Pending".
4. **Download (internal + portal if present):** require `VirusScanStatus == "Clean"`; block everything else (404/NotFound). This is the core gate.
5. **Guards:** LEAVE DocumentEvidenceGuard / FlagGuards as-is (Clean passes; uploads are now Clean-or-rejected; seed docs with null status still pass) — do NOT tighten guards this slice (would risk breaking workflow guard tests; out of scope).

## Edge cases (enumerated up front)
1. **EICAR-in-PDF must pass the magic-byte check** so the E2E exercises the VIRUS gate, not the magic-byte gate: build the test payload as a valid PDF header (`%PDF-1.4…`) with the EICAR signature embedded in the body. EicarVirusScanner scans the whole content for the signature.
2. **Fail-closed:** anything not "Clean" (Pending/Infected/Error/null) blocks download.
3. **Do not break workflow guard tests** — keep guards accepting non-Infected; uploads being Clean-or-rejected keeps them consistent.
4. **Existing `Tests/Controllers/DocumentControllerTests.cs`** likely constructs the controller without a scanner and may assert "Pending" — update construction + assertions to the new behavior WITHOUT weakening them.
5. In-memory scan of the uploaded stream is fine (10 MB cap already enforced).

## Acceptance criteria
- `IVirusScanner` + `EicarVirusScanner` + DI registration.
- Uploads scanned (no more hard "Pending"): Infected rejected, Clean persisted — internal AND portal.
- Downloads require "Clean" (internal AND portal if it has download).
- Unit tests (scanner EICAR detection; download Clean-gate; upload rejects EICAR) + a Playwright E2E (validator uploads a clean PDF → downloadable; uploads a PDF carrying EICAR → blocked). E2E reads a seeded FileMaster id by opening an ApplicationDBContext against `KestrelAppFixture.E2EConnectionString`.
- Full suite green (baseline 506 unit + 14 E2E). Build clean. DWS palette on any view change.

## Off-limits
Workflow guard logic; the export/reporting code; `.worktrees/`, `.claude/worktrees/`; E2E shared infra files (only ADD a test class).

## Journal

### 2026-06-08 — controller — task setup + design
- Read Document model, internal + portal DocumentControllers (upload Pending + download Infected-only gate), DocumentEvidenceGuard/FlagGuards, Services/Documents.
- Design above. Single serial implementer (dotnet specialist) covering abstraction + scanner + both controllers + DI + unit + E2E; controller verifies.
- Status: DONE (setup)

### 2026-06-08 — implementer (agent, across 2 truncated passes) — core implementation
- `Services/Documents/IVirusScanner.cs` (+ `VirusScanResult` Clean/Infected/Error) + `EicarVirusScanner.cs` (flags the EICAR signature; ClamAV/Defender seam noted). Registered in `Program.cs:189` (singleton).
- Internal `Controllers/DocumentController.cs`: scans on upload (fail-closed — Infected/Error rejected, no persist), persists `VirusScanStatus="Clean"`; Download now `if (doc.VirusScanStatus != "Clean") return NotFound();` (:150).
- External `Areas/ExternalPortal/Controllers/DocumentController.cs`: same upload scan + Clean persist. (Portal has NO uploaded-document download path — public users download only app-generated LETTERS via CaseController; nothing to gate there.)
- `Views/Document/Upload.cshtml` (upload-submit id), `Tests/Controllers/DocumentControllerTests.cs` + `Tests/Areas/ExternalPortal/DocumentControllerTests.cs` updated; `Tests/Services/Documents/EicarVirusScannerTests.cs` added.
- Agent truncated 3× (twice mid-narration) and never wrote DI / portal / E2E in one pass; controller resumed it once (it then landed DI + portal + tests) and finished the E2E itself. Per Rule 3, stopped re-dispatching an agent that couldn't deliver and took over.

### 2026-06-08 — controller — completed E2E + verified handoff (Rule 4)
- Wrote `Tests.E2E/DocumentVirusScanTests.cs` (agent kept truncating before it): (1) Validator uploads a CLEAN PDF → persisted Clean → downloaded via authenticated APIRequest returns `%PDF` (200); (2) uploads a valid-PDF wrapper carrying the EICAR signature (passes magic-byte, scanner flags) → rejected on the Upload view with "failed virus scanning", and a DB check confirms NO Document row persisted. Targets `#upload-submit` (NOT button[type=submit] — layout Sign out trap).
- Verified myself: build 0 errors; `dotnet test Tests.E2E` → **16/16**; `dotnet test Tests` → **516/516** (consistently green on 3 of 4 runs).
- **Honest caveat — flaky integration tests:** one of four unit-suite runs showed a single transient failure (then 516/516 on the next three). Root signature: the SQL-backed integration tests run in parallel `WebApplicationFactory` instances all calling `MigrateAsync` against the shared dev DB `dwa_val_ver` — concurrent-migration contention (the X-01 dev-DB-sharing weakness), NOT a DOC-02 defect. Follow-up: put the SQL-backed integration tests in a single non-parallel xUnit collection.
- Status: DONE (DOC-02 verified GREEN; gate proven end-to-end)

## Retro (on completion)
**Converged:** the security design was right and the gate works end-to-end — the EICAR E2E genuinely uploads malware-signature content through the real browser flow and proves it's rejected and unservable. **Drifted:** this agent truncated three times and never reached DI registration / the portal controller / the E2E on its own — the implementation would have shipped with an unregistered IVirusScanner (runtime 500) had the controller trusted the first "in progress" narration. Resuming once recovered DI+portal; the controller wrote the E2E itself rather than burn another round. **Durable lesson:** a backgrounded agent that returns only mid-narration is NOT done — verify on disk (grep for the wiring: DI registration, every controller touched, the test files) before accepting; and when an agent can't converge after one resume, the controller finishes the focused remainder rather than re-dispatching indefinitely. Also surfaced a real pre-existing flake (parallel SQL integration tests on the shared dev DB) worth a small follow-up.
