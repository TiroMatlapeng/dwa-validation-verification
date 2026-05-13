using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc.Rendering;
using Xunit;

namespace dwa_ver_val.Tests.ViewModels.Admin;

// Regression: the Add/Edit User pages threw at view-render time because
// AvailableOrgUnits was an IEnumerable<(Guid Id, string Name)> tuple and the
// view did `new SelectList(model.AvailableOrgUnits, "Id", "Name")`. ValueTuple
// element names are erased at runtime — the runtime properties are Item1/Item2
// — so SelectList's reflection lookup of "Id"/"Name" failed when iterated.
// The fix replaced the tuple with the OrgUnitOption POCO. These tests pin the
// property contract so the bug cannot silently come back.
public class UserAdminViewModelTests
{
    [Fact]
    public void CreateUserViewModel_AvailableOrgUnits_IsBindableBySelectListByIdAndName()
    {
        var unitId = Guid.NewGuid();
        var model = new CreateUserViewModel
        {
            AvailableOrgUnits = new[]
            {
                new OrgUnitOption { Id = unitId, Name = "Inkomati-Usuthu CMA" }
            }
        };

        var list = new SelectList(model.AvailableOrgUnits, "Id", "Name").ToList();

        var item = Assert.Single(list);
        Assert.Equal(unitId.ToString(), item.Value);
        Assert.Equal("Inkomati-Usuthu CMA", item.Text);
    }

    [Fact]
    public void EditUserViewModel_AvailableOrgUnits_IsBindableBySelectListByIdAndName()
    {
        var unitId = Guid.NewGuid();
        var model = new EditUserViewModel
        {
            AvailableOrgUnits = new[]
            {
                new OrgUnitOption { Id = unitId, Name = "Olifants WMA" }
            }
        };

        var list = new SelectList(model.AvailableOrgUnits, "Id", "Name").ToList();

        var item = Assert.Single(list);
        Assert.Equal(unitId.ToString(), item.Value);
        Assert.Equal("Olifants WMA", item.Text);
    }
}
