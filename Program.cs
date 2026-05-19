using dwa_ver_val.Services.Infrastructure.Email;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

// QuestPDF licence — Community for the demo build. See docs/superpowers/specs/2026-04-24-mvp-hardening-design.md §4.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new PortalAuthorizationConvention());
});

// DbContext
builder.Services.AddDbContext<ApplicationDBContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity (cookie auth; no Identity UI scaffolding)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDBContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Build the auth scheme list incrementally:
//   - PublicPortalScheme cookie (always present, for /Areas/ExternalPortal/*).
//   - Microsoft (Entra ID) OIDC (added only when env vars are configured).
// The default scheme remains Identity.Application, set by AddIdentity above.
var authBuilder = builder.Services.AddAuthentication();

builder.Services.AddScoped<PortalCookieEvents>();
authBuilder.AddCookie(PortalCookieOptions.SchemeName, options =>
{
    PortalCookieOptions.Configure(options);
    options.EventsType = typeof(PortalCookieEvents);
});

var entraTenantId = builder.Configuration["AzureAd:TenantId"];
var entraClientId = builder.Configuration["AzureAd:ClientId"];
var entraClientSecret = builder.Configuration["AzureAd:ClientSecret"];
if (!string.IsNullOrEmpty(entraTenantId) && !string.IsNullOrEmpty(entraClientId) && !string.IsNullOrEmpty(entraClientSecret))
{
    authBuilder.AddOpenIdConnect("Microsoft", "Microsoft", options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{entraTenantId}/v2.0";
        options.ClientId = entraClientId;
        options.ClientSecret = entraClientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = builder.Configuration["AzureAd:CallbackPath"] ?? "/signin-oidc";
        options.SignedOutCallbackPath = builder.Configuration["AzureAd:SignedOutCallbackPath"] ?? "/signout-callback-oidc";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters.NameClaimType = "name";
    });
}

// Claims transformation (populates role + scope claims on every request)
builder.Services.AddScoped<IClaimsTransformation, DwsClaimsTransformation>();

// Authorisation policies — single AddAuthorization merges DWS-staff and portal policies.
builder.Services.AddAuthorization(options =>
{
    DwsPolicies.Configure(options);
    PortalPolicies.Configure(options);
});

// Repository DI
builder.Services.AddScoped<IPropertyInterface, PropertyRepository>();
builder.Services.AddScoped<IAddress, AddressRepository>();
builder.Services.AddScoped<IFileMaster, FileMasterRepository>();
builder.Services.AddScoped<IForestation, ForestationRepository>();
builder.Services.AddScoped<IFieldAndCrop, FieldAndCropRepository>();
builder.Services.AddScoped<IDamCalculation, DamCalculationRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IScopedCaseQuery, ScopedCaseQuery>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<dwa_ver_val.Services.Calculator.ICalculatorService, dwa_ver_val.Services.Calculator.CalculatorService>();
builder.Services.AddScoped<ILawfulnessAssessmentService, LawfulnessAssessmentService>();

// Workflow transition guards — evaluated in registration order by WorkflowService.MoveToStateAsync.
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp2SpatialInfoGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp3WarmsReviewedGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp4AdditionalInfoGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp5MapbookPresentGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp6FieldCropGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp7EluGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp8DamOrNAGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp9SfraOrNAGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.CpPrePublicReviewGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.CpStakeholderWorkshopGuard>();

// Letter generation (Plan 4)
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter1Template>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter1ATemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter2Template>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter2ATemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter3Template>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter4ATemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S35Letter4_5Template>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S33_2DeclarationTemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S33_3aDeclarationTemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplate, dwa_ver_val.Services.Letters.Templates.S33_3bDeclarationTemplate>();
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterTemplateRegistry, dwa_ver_val.Services.Letters.LetterTemplateRegistry>();
builder.Services.AddSingleton<dwa_ver_val.Services.Letters.IPdfRenderer, dwa_ver_val.Services.Letters.QuestPdfRenderer>();
builder.Services.AddSingleton<dwa_ver_val.Services.Letters.IBlobStore>(sp =>
    new dwa_ver_val.Services.Letters.FileSystemBlobStore(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "_uploads")));
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterService, dwa_ver_val.Services.Letters.LetterService>();

// Portal infrastructure abstractions
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    new LocalDiskFileStorage(
        Path.Combine(builder.Environment.ContentRootPath, "portal-uploads")));
builder.Services.AddScoped<IPublicUserPropertyAccessor, PublicUserPropertyAccessor>();
builder.Services.AddScoped<IPublicUserRegistrationService, PublicUserRegistrationService>();
builder.Services.AddScoped<IPublicUserSignInService, PublicUserSignInService>();
builder.Services.AddSingleton<PasswordHasher<PublicUser>>();
builder.Services.AddHttpContextAccessor();

// Portal exception handler + ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PortalExceptionHandler>();

// Portal rate limiter
builder.Services.AddRateLimiter(PortalRateLimitPolicies.Configure);

// Seeders
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<IdentitySeeder>();

var app = builder.Build();

// POPIA guard: refuse to start in Production while PublicUser.IdentityNumber is
// stored unencrypted, unless an explicit acknowledgement flag is set. This
// prevents accidental promotion to prod before Task 10.3 wires DataProtection
// encryption. See design spec §5.6.
if (app.Environment.IsProduction()
    && !builder.Configuration.GetValue<bool>("Portal:AllowPlaintextIdentityNumber"))
{
    throw new InvalidOperationException(
        "PublicUser.IdentityNumber is stored unencrypted. Set " +
        "Portal:AllowPlaintextIdentityNumber=true to acknowledge for non-production data, " +
        "or wire IDataProtectionProvider encryption (Task 10.3).");
}

// Warn if LoggingEmailSender is in use under Production — emails will leak
// PII (user names, confirmation links) into the log sink. Replace before
// production go-live.
if (app.Environment.IsProduction())
{
    var emailSender = app.Services.GetRequiredService<IEmailSender>();
    if (emailSender is LoggingEmailSender)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        startupLogger.LogWarning(
            "LoggingEmailSender is active under Production. Email bodies (containing PII) will be written to the log. Wire a real IEmailSender before any production data lands.");
    }
}

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    await db.Database.MigrateAsync();

    var refSeeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await refSeeder.SeedAsync();

    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();
}

// Pipeline
app.UseExceptionHandler();          // routes through the IExceptionHandler chain (PortalExceptionHandler first)

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Honour X-Forwarded-For so RemoteIpAddress is the real client IP behind
// Azure App Service / front door. PortalRateLimitPolicies partitions by IP;
// without this, every external user collapses onto a single proxy partition
// and one attacker would trip a global lockout. Must run BEFORE UseRouting.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseRouting();

app.UseRateLimiter();               // must run between UseRouting and UseAuthentication

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Area route — must be registered BEFORE the default route so Areas resolve.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose the Program class for Microsoft.AspNetCore.Mvc.Testing
public partial class Program { }
