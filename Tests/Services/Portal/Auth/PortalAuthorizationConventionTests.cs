using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using dwa_ver_val.Services.Portal.Auth;
using System.Reflection;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PortalAuthorizationConventionTests
{
    [Area("ExternalPortal")]
    private sealed class FakePortalController : Controller { public IActionResult X() => Ok(); }

    private sealed class FakeNonPortalController : Controller { public IActionResult X() => Ok(); }

    private static ControllerModel BuildControllerModel(Type controllerType)
    {
        var typeInfo = controllerType.GetTypeInfo();
        var attrs = typeInfo.GetCustomAttributes(inherit: true);
        var model = new ControllerModel(typeInfo, attrs);
        var actionMethod = controllerType.GetMethod("X")!;
        var actionAttrs = actionMethod.GetCustomAttributes(inherit: true);
        var action = new ActionModel(actionMethod, actionAttrs) { Controller = model };
        model.Actions.Add(action);
        return model;
    }

    [Fact]
    public void Apply_AddsAuthorizeFilter_WhenControllerIsInExternalPortalArea()
    {
        var convention = new PortalAuthorizationConvention();
        var model = BuildControllerModel(typeof(FakePortalController));

        convention.Apply(model);

        var filter = Assert.Single(model.Filters.OfType<AuthorizeFilter>());
        var policy = filter.Policy ?? filter.AuthorizeData?.Aggregate(
            new AuthorizationPolicyBuilder(),
            (b, d) => { if (d.AuthenticationSchemes is { Length: > 0 } s) b.AddAuthenticationSchemes(s.Split(',')); if (d.Policy is { Length: > 0 } p) b.AddRequirements(new DenyAnonymousAuthorizationRequirement()); return b; }).Build();
        Assert.NotNull(policy);
    }

    [Fact]
    public void Apply_DoesNothing_WhenControllerIsOutsideExternalPortalArea()
    {
        var convention = new PortalAuthorizationConvention();
        var model = BuildControllerModel(typeof(FakeNonPortalController));

        convention.Apply(model);

        Assert.Empty(model.Filters.OfType<AuthorizeFilter>());
    }
}
