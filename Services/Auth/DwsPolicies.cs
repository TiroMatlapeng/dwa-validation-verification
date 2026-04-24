using Microsoft.AspNetCore.Authorization;

public static class DwsPolicies
{
    public const string CanAdminister = "CanAdminister";
    public const string CanCreateCase = "CanCreateCase";
    public const string CanTransitionWorkflow = "CanTransitionWorkflow";
    public const string CanIssueLetter = "CanIssueLetter";
    public const string CanCapture = "CanCapture";
    public const string CanRead = "CanRead";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(CanAdminister,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AdminOnly));

        options.AddPolicy(CanCreateCase,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastValidator));

        options.AddPolicy(CanTransitionWorkflow,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastValidator));

        options.AddPolicy(CanIssueLetter,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastRegionalManager));

        options.AddPolicy(CanCapture,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastCapturer));

        options.AddPolicy(CanRead,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastReadOnly));
    }
}
