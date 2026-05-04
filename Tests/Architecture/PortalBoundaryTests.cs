using NetArchTest.Rules;
using Xunit;

namespace dwa_ver_val.Tests.Architecture;

public class PortalBoundaryTests
{
    private static System.Reflection.Assembly AppAssembly => typeof(Program).Assembly;

    [Fact]
    public void PortalServices_MustNotReferenceIdentityUserManager()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .ResideInNamespace("dwa_ver_val.Services.Portal")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore.Identity.UserManager`1",
                "Microsoft.AspNetCore.Identity.SignInManager`1")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Services/Portal types must not depend on UserManager<ApplicationUser> or SignInManager<ApplicationUser>. " +
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void ExternalPortalArea_MustNotReferenceIdentityUserManager()
    {
        var result = Types.InAssembly(AppAssembly)
            .That()
            .ResideInNamespace("dwa_ver_val.Areas.ExternalPortal")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore.Identity.UserManager`1",
                "Microsoft.AspNetCore.Identity.SignInManager`1")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Areas/ExternalPortal types must not depend on UserManager<ApplicationUser> or SignInManager<ApplicationUser>. " +
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
