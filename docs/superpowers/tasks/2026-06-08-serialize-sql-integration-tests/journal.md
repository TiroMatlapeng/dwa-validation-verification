# Task: Serialize SQL-backed integration tests (Slice 6 — flake fix)

**Start:** 2026-06-08
**Branch:** fix/remediation-wave4 (main working tree)
**Why:** During DOC-02 the unit suite flaked — 1 of 4 runs showed a single transient failure (green the other 3). Root cause: the 6 `WebApplicationFactory`-based integration test classes (4 `IClassFixture<IntegrationTestFixture>` + 2 `IClassFixture<PortalIntegrationTestFixture>`) each call `db.Database.MigrateAsync()` against the shared dev DB `dwa_val_ver` on host startup, and xUnit runs distinct classes in parallel → two could migrate the same DB at the same instant → intermittent concurrent-migration failure (the X-01 shared-dev-DB weakness).

**Fix (solo — controller; small mechanical test-infra change):**
- Added `Tests/Integration/SqlServerIntegrationCollection.cs` — `[CollectionDefinition("SqlServerIntegration")]` marker (no shared fixture).
- Decorated all 8 SQL-touching test classes with `[Collection(SqlServerIntegrationCollection.Name)]`: the 6 WebApplicationFactory classes (StaticFileExposure, AuthAudit, IdentityFlow, ForgotPasswordFlow, PortalRegistrationFlow, PortalCookieSchemeIsolation) + the 2 dedicated-DB race tests (WorkflowConcurrency, LetterIssuanceRace — folded in from their solo collections). Tests in one collection never run in parallel with each other, so no two SQL tests overlap. The fast InMemory unit tests stay in their own implicit parallel collections — suite speed barely changes.

**Verification:** build clean; **6 consecutive full runs of `dotnet test Tests` → 516/516 every time** (previously ~1-in-4 flaked). E2E unaffected (separate assembly; 16/16 stands).

**Note / deeper follow-up:** the underlying X-01 weakness remains — the integration tests still migrate the shared *dev* DB (`dwa_val_ver`) rather than a dedicated `dwa_val_ver_inttest`. Serialization removes the concurrency flake; isolating onto a dedicated integration DB (so dev data can never affect tests, à la the WF-02 duplicate-letter surprise) is the larger X-01 fix for a later slice.

## Retro
Clean, surgical, high-leverage: the regression net the whole month leans on is now reliably green. Done solo because it was small and mechanical and the agents had been truncating — the controller-finishes-the-focused-remainder pattern from DOC-02 applied here too.
