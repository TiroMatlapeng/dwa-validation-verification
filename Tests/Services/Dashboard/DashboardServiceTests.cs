using System.Security.Claims;
using dwa_ver_val.Services.Dashboard;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Dashboard;

public class DashboardServiceTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DashboardService Svc(ApplicationDBContext db)
    {
        var scope = new ScopedCaseQuery(db);
        var reporting = new ReportingService(db, scope, new MemoryCache(new MemoryCacheOptions()));
        return new DashboardService(db, scope, reporting);
    }

    private static ClaimsPrincipal NationalManager(Guid uid) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
            new Claim(ClaimTypes.Role, DwsRoles.NationalManager)
        }, "Test"));

    private static ClaimsPrincipal RegionalManager(Guid uid, Guid wmaId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
            new Claim(ClaimTypes.Role, DwsRoles.RegionalManager),
            new Claim("wmaId", wmaId.ToString())
        }, "Test"));

    private static Property Prop(ApplicationDBContext db, Guid wmaId)
    {
        var p = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P", SGCode = "SG", WmaId = wmaId };
        db.Properties.Add(p);
        return p;
    }

    private static FileMaster Case(ApplicationDBContext db, Guid propertyId, string status) => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = propertyId, ValidationStatusName = status,
        RegistrationNumber = "WARMS-1", SurveyorGeneralCode = "SG", PrimaryCatchment = "A21",
        QuaternaryCatchment = "A21A", FarmName = "F", FarmNumber = 1,
        RegistrationDivision = "TD", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task Kpis_And_ValidationStatusChart_AreScopedAndCounted()
    {
        using var db = NewDb();
        var wmaA = Guid.NewGuid(); var wmaB = Guid.NewGuid();
        var pA = Prop(db, wmaA); var pB = Prop(db, wmaB);
        db.FileMasters.Add(Case(db, pA.PropertyId, "Completed"));
        db.FileMasters.Add(Case(db, pA.PropertyId, "In Process"));
        db.FileMasters.Add(Case(db, pB.PropertyId, "Completed")); // other WMA
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(RegionalManager(Guid.NewGuid(), wmaA), CancellationToken.None);

        Assert.Equal(2, vm.CompletedOrInProcessTotal()); // only WMA-A's two cases
        Assert.Equal(1, vm.CompletedCases);
        Assert.Equal(1, vm.InProcessCases);
        Assert.Equal(1, vm.TotalProperties); // only WMA-A property
        Assert.Contains(vm.ValidationStatusChart, p => p.Label == "Completed" && p.Value == 1);
        Assert.Contains(vm.ValidationStatusChart, p => p.Label == "In Process" && p.Value == 1);
    }

    [Fact]
    public async Task PhaseChart_CountsCasesByWorkflowPhase_AndNotStarted()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var p = Prop(db, wma);
        var validationState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP3_WARMSEvaluation", Phase = "Validation", DisplayOrder = 9 };
        db.WorkflowStates.Add(validationState);

        var withInstance = Case(db, p.PropertyId, "In Process");
        var notStarted = Case(db, p.PropertyId, "Not Commenced");
        db.FileMasters.AddRange(withInstance, notStarted);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(), FileMasterId = withInstance.FileMasterId,
            CurrentWorkflowStateId = validationState.WorkflowStateId, Status = "Active",
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(NationalManager(Guid.NewGuid()), CancellationToken.None);

        Assert.Contains(vm.PhaseChart, x => x.Label == "Validation" && x.Value == 1);
        Assert.Contains(vm.PhaseChart, x => x.Label == "Not Started" && x.Value == 1);
    }

    [Fact]
    public async Task MyTasks_OnlyReturnsCasesAssignedToCurrentUser()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var p = Prop(db, wma);
        var me = Guid.NewGuid(); var other = Guid.NewGuid();
        var state = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_GISAnalysis", Phase = "Validation", DisplayOrder = 11 };
        db.WorkflowStates.Add(state);

        var mine = Case(db, p.PropertyId, "In Process");
        var theirs = Case(db, p.PropertyId, "In Process");
        db.FileMasters.AddRange(mine, theirs);
        db.WorkflowInstances.AddRange(
            new WorkflowInstance { WorkflowInstanceId = Guid.NewGuid(), FileMasterId = mine.FileMasterId, CurrentWorkflowStateId = state.WorkflowStateId, AssignedToId = me, Status = "Active", CreatedDate = DateTime.UtcNow },
            new WorkflowInstance { WorkflowInstanceId = Guid.NewGuid(), FileMasterId = theirs.FileMasterId, CurrentWorkflowStateId = state.WorkflowStateId, AssignedToId = other, Status = "Active", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(NationalManager(me), CancellationToken.None);

        var task = Assert.Single(vm.MyTasks);
        Assert.Equal("WARMS-1", task.CaseReference);
        Assert.Equal("CP5_GISAnalysis", task.CurrentState);
    }
}
