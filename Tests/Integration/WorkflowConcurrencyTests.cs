using dwa_ver_val.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// SQL-Server-backed concurrency tests for WorkflowInstance.RowVersion.
///
/// WHY THESE CANNOT USE InMemory:
/// EF InMemory ignores concurrency tokens — DbUpdateConcurrencyException will never
/// be thrown against InMemory regardless of RowVersion configuration. These tests
/// must run against a real SQL Server rowversion column.
///
/// DATABASE: dedicated isolated database "dwa_val_ver_wftest" — wiped before each
/// test class run via EnsureDeleted + EnsureCreated. Never touches dwa_val_ver (dev).
///
/// PREREQUISITE: SQL Server must be reachable at localhost,1433 (docker dev stack).
/// If unreachable the tests fail with a SqlException, NOT a silent skip — per design.
/// </summary>
[Collection("WorkflowConcurrency")]
public class WorkflowConcurrencyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=dwa_val_ver_wftest;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

    private static DbContextOptions<ApplicationDBContext> BuildOptions() =>
        new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseSqlServer(ConnectionString)
            .Options;

    private ApplicationDBContext CreateContext() => new ApplicationDBContext(BuildOptions());

    // Seeded once per test class lifetime (IAsyncLifetime.InitializeAsync).
    private Guid _fileMasterId;
    private Guid _workflowInstanceId;
    private Guid _state1Id;
    private Guid _state2Id;

    // ── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Wipe and recreate the isolated test database.
        // EnsureCreated uses the EF model (not migration history), which includes
        // the RowVersion column from WorkflowInstance.RowVersion + IsRowVersion() config.
        await using var seedCtx = CreateContext();
        await seedCtx.Database.EnsureDeletedAsync();
        await seedCtx.Database.EnsureCreatedAsync();

        // Seed: two workflow states + a Property + a FileMaster + a WorkflowInstance.
        _state1Id = Guid.NewGuid();
        _state2Id = Guid.NewGuid();

        var state1 = new WorkflowState
        {
            WorkflowStateId = _state1Id,
            StateName = "WFTest_State1",
            Phase = "Inception",
            DisplayOrder = 1,
            IsTerminal = false,
        };
        var state2 = new WorkflowState
        {
            WorkflowStateId = _state2Id,
            StateName = "WFTest_State2",
            Phase = "Inception",
            DisplayOrder = 2,
            IsTerminal = false,
        };
        seedCtx.WorkflowStates.AddRange(state1, state2);

        var property = new Property { PropertyId = Guid.NewGuid() };
        seedCtx.Properties.Add(property);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "WFT-001",
            SurveyorGeneralCode = "SG-WFT",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A99Z",
            FarmName = "TestFarm",
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
        };
        seedCtx.FileMasters.Add(fm);
        await seedCtx.SaveChangesAsync();

        _fileMasterId = fm.FileMasterId;

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = _fileMasterId,
            CurrentWorkflowStateId = _state1Id,
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
        };
        seedCtx.WorkflowInstances.Add(instance);

        // Initial step record (mirrors WorkflowService.StartWorkflowAsync).
        seedCtx.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = _state1Id,
            StepStatus = "InProgress",
            StartedDate = DateTime.UtcNow,
        });

        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await seedCtx.SaveChangesAsync();

        _workflowInstanceId = instance.WorkflowInstanceId;
    }

    public async Task DisposeAsync()
    {
        // Drop the test database after all tests in this collection complete.
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
    }

    // ── DbContext-level race test (mandatory minimum) ───────────────────────

    /// <summary>
    /// Two separate DbContext instances load the SAME WorkflowInstance.
    /// ctx1 commits a CurrentWorkflowStateId change — SQL Server bumps the rowversion.
    /// ctx2 then tries to commit the same entity with its now-stale rowversion.
    /// Assert: ctx2.SaveChangesAsync throws DbUpdateConcurrencyException; the persisted
    /// state reflects only ctx1's change.
    /// </summary>
    [Fact]
    public async Task RowVersion_StaleContext_ThrowsDbUpdateConcurrencyException()
    {
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        // Both contexts load the same WorkflowInstance independently (no shared identity map).
        var instance1 = await ctx1.WorkflowInstances.FindAsync(_workflowInstanceId);
        var instance2 = await ctx2.WorkflowInstances.FindAsync(_workflowInstanceId);

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);

        // Both are pointing at the same row; rowversion values should be identical.
        Assert.Equal(instance1.RowVersion, instance2.RowVersion);

        // ctx1 wins: move to state2.
        instance1.CurrentWorkflowStateId = _state2Id;
        await ctx1.SaveChangesAsync(); // Succeeds; SQL Server bumps RowVersion.

        // ctx2 still has the old RowVersion. Any change + SaveChanges must fail.
        instance2.CurrentWorkflowStateId = _state2Id;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctx2.SaveChangesAsync());

        // Verify: the persisted state reflects ctx1's change only.
        await using var verifyCtx = CreateContext();
        var persisted = await verifyCtx.WorkflowInstances.FindAsync(_workflowInstanceId);
        Assert.NotNull(persisted);
        Assert.Equal(_state2Id, persisted.CurrentWorkflowStateId);
    }

    // ── Service-level race test (proves domain exception surfaces correctly) ─

    /// <summary>
    /// Two WorkflowService instances (each backed by its own DbContext, sharing the same SQL DB)
    /// both call AdvanceAsync for the same FileMaster. Exactly one succeeds; the other
    /// throws WorkflowConcurrencyException. After the race: exactly ONE step record was added
    /// for the transition (two would indicate a double-advance).
    ///
    /// Note: requires the workflow instance to still be at state1.
    /// This test creates a fresh FileMaster+WorkflowInstance so it is independent of the
    /// DbContext-level test above (which already advanced the shared instance to state2).
    /// </summary>
    [Fact]
    public async Task ServiceAdvance_ConcurrentCall_ExactlyOneSucceeds_OtherThrowsWorkflowConcurrencyException()
    {
        // Seed a second, isolated FileMaster + WorkflowInstance for this test.
        Guid raceFmId;
        Guid raceInstanceId;

        await using (var setupCtx = CreateContext())
        {
            var fm2 = new FileMaster
            {
                FileMasterId = Guid.NewGuid(),
                PropertyId = (await setupCtx.Properties.FirstAsync()).PropertyId,
                RegistrationNumber = "WFT-RACE",
                SurveyorGeneralCode = "SG-RACE",
                PrimaryCatchment = "A",
                QuaternaryCatchment = "A99Z",
                FarmName = "RaceFarm",
                FarmNumber = 2,
                RegistrationDivision = "JR",
                FarmPortion = "0",
                FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
            };
            setupCtx.FileMasters.Add(fm2);
            await setupCtx.SaveChangesAsync();

            var raceInstance = new WorkflowInstance
            {
                WorkflowInstanceId = Guid.NewGuid(),
                FileMasterId = fm2.FileMasterId,
                CurrentWorkflowStateId = _state1Id,
                Status = "Active",
                CreatedDate = DateTime.UtcNow,
            };
            setupCtx.WorkflowInstances.Add(raceInstance);
            setupCtx.WorkflowStepRecords.Add(new WorkflowStepRecord
            {
                WorkflowStepRecordId = Guid.NewGuid(),
                WorkflowInstanceId = raceInstance.WorkflowInstanceId,
                WorkflowStateId = _state1Id,
                StepStatus = "InProgress",
                StartedDate = DateTime.UtcNow,
            });
            fm2.WorkflowInstanceId = raceInstance.WorkflowInstanceId;
            await setupCtx.SaveChangesAsync();

            raceFmId = fm2.FileMasterId;
            raceInstanceId = raceInstance.WorkflowInstanceId;
        }

        // Build two WorkflowService instances over two separate contexts.
        var ctx1 = CreateContext();
        var ctx2 = CreateContext();
        var guards = Array.Empty<ITransitionGuard>();
        var audit = new dwa_ver_val.Tests.Helpers.TestAuditService();

        var svc1 = new WorkflowService(ctx1, guards, audit);
        var svc2 = new WorkflowService(ctx2, guards, audit);

        // --- Simulate the race ---
        // Both services load the instance to advance (done implicitly inside AdvanceAsync).
        // We need to ensure both read the entity BEFORE either saves. We do this by calling
        // AdvanceAsync on svc1, completing it, then calling svc2.AdvanceAsync — svc2's context
        // already has a tracked (stale) copy from the initial SaveChangesAsync of the setup.
        //
        // A more realistic approach: load the instance in both contexts simultaneously,
        // then advance svc1, then advance svc2 (whose tracked entity is now stale).
        // We force both contexts to pre-load the instance using GetInstanceForFileAsync.
        var _ = await svc1.GetInstanceForFileAsync(raceFmId);  // pre-loads into ctx1 tracker
        var __ = await svc2.GetInstanceForFileAsync(raceFmId); // pre-loads into ctx2 tracker

        // svc1 wins.
        var winnerResult = await svc1.AdvanceAsync(raceFmId, userId: null, notes: null);
        Assert.Equal(_state2Id, winnerResult.CurrentWorkflowStateId);

        // svc2 now has a stale RowVersion on its tracked instance.
        var concurrencyEx = await Assert.ThrowsAsync<WorkflowConcurrencyException>(
            () => svc2.AdvanceAsync(raceFmId, userId: null, notes: null));

        Assert.Contains("another user", concurrencyEx.Message, StringComparison.OrdinalIgnoreCase);

        // Verify: exactly one advance's step records exist for the race instance.
        // MoveToStateAsync updates the existing InProgress step to Completed (same row)
        // and inserts one new InProgress step for the target state.
        // 1 (initial InProgress, now Completed) + 1 (new InProgress for state2) = 2 total.
        // A double-advance would leave 3 records (original + two new InProgress inserts).
        await using var verifyCtx = CreateContext();
        var stepCount = await verifyCtx.WorkflowStepRecords
            .Where(s => s.WorkflowInstanceId == raceInstanceId)
            .CountAsync();
        Assert.Equal(2, stepCount); // initial (now Completed) + one new InProgress from the winner

        var persistedInstance = await verifyCtx.WorkflowInstances.FindAsync(raceInstanceId);
        Assert.NotNull(persistedInstance);
        Assert.Equal(_state2Id, persistedInstance.CurrentWorkflowStateId);

        await ctx1.DisposeAsync();
        await ctx2.DisposeAsync();
    }
}
