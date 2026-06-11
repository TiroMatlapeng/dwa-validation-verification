using dwa_ver_val.Services.Infrastructure.Email;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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

    // BUG-001: Insert before the default SimpleTypeModelBinderProvider so all
    // decimal / decimal? bindings parse with InvariantCulture, independent of
    // the host OS culture (e.g. en_ZA, which uses comma as decimal separator).
    options.ModelBinderProviders.Insert(0, new dwa_ver_val.Infrastructure.InvariantDecimalModelBinderProvider());
});

// DbContext
builder.Services.AddDbContext<ApplicationDBContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Data Protection — persist keys to SQL Server so antiforgery and auth cookies
// stay valid across pod restarts on AKS (otherwise each new pod regenerates
// ephemeral keys and existing browser sessions get HTTP 400 on form submit).
// SetApplicationName must remain stable across all pods/deployments.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDBContext>()
    .SetApplicationName("dwa-ver-val");

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
    // SameAsRequest: cookie is marked Secure only when the request itself is
    // HTTPS. The AKS pod terminates TLS at the ingress and speaks plain HTTP,
    // so Always would set Secure and the browser would refuse to return the
    // auth cookie over HTTP — causing an immediate logout/redirect loop after
    // a successful login. With HTTPS in front, this still marks cookies Secure.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp11FileCompilationGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp19PajaChecklistGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.LetterServiceConfirmedGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.DocumentEvidenceGuard>();

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
        Path.Combine(builder.Environment.ContentRootPath, "letter-blobs")));
// SEC/DOC-01: letter blobs are stored outside wwwroot so UseStaticFiles() cannot
// serve them. Access is exclusively through the scope-checked FileMasterController.LetterPdf
// action which resolves bytes via IBlobStore.ReadAsync.
builder.Services.AddScoped<dwa_ver_val.Services.Letters.ILetterService, dwa_ver_val.Services.Letters.LetterService>();

// Reporting — memory cache + exporters + reporting service
builder.Services.AddMemoryCache();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.IReportingService, dwa_ver_val.Services.Reporting.ReportingService>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.CsvReportExporter>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.ExcelReportExporter>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.PdfReportExporter>();
builder.Services.AddScoped<dwa_ver_val.Services.Dashboard.IDashboardService, dwa_ver_val.Services.Dashboard.DashboardService>();

// Portal infrastructure abstractions
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
var smtpHost = builder.Configuration["SmtpSettings:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    new LocalDiskFileStorage(
        Path.Combine(builder.Environment.ContentRootPath, "portal-uploads")));
// DOC-02: virus scanner behind IVirusScanner. EicarVirusScanner is the stateless default
// (detects the standard EICAR test signature); swap for ClamAV/Defender via config later.
builder.Services.AddSingleton<dwa_ver_val.Services.Documents.IVirusScanner,
    dwa_ver_val.Services.Documents.EicarVirusScanner>();
builder.Services.AddScoped<dwa_ver_val.Services.Notifications.INotificationService,
    dwa_ver_val.Services.Notifications.NotificationService>();
builder.Services.AddScoped<IPublicUserPropertyAccessor, PublicUserPropertyAccessor>();
builder.Services.AddScoped<IPublicUserRegistrationService, PublicUserRegistrationService>();
builder.Services.AddScoped<IPublicUserSignInService, PublicUserSignInService>();
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.IDeviceTrustService, dwa_ver_val.Services.Portal.Mfa.DeviceTrustService>();
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.ITotpService, dwa_ver_val.Services.Portal.Mfa.TotpService>();
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.ISmsOtpService, dwa_ver_val.Services.Portal.Mfa.SmsOtpService>();
// TODO: Replace LoggingSmsGateway with a real SMS provider (BulkSMS, Twilio, etc.)
// before user-acceptance testing. Configure credentials in appsettings and register
// the real implementation here using a config check.
builder.Services.AddSingleton<dwa_ver_val.Services.Portal.Mfa.ISmsGateway, dwa_ver_val.Services.Portal.Mfa.LoggingSmsGateway>();
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

// Only emit HSTS when the pod is actually listening on HTTPS (or proxied as HTTPS).
// On AKS the pod speaks plain HTTP — TLS terminates at the ingress — so emitting
// HSTS over HTTP instructs browsers to require HTTPS for the IP/domain for the
// next 30 days, which permanently breaks plain-HTTP dev deployments.
if (!app.Environment.IsDevelopment()
    && !string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"])
    && builder.Configuration["ASPNETCORE_URLS"]!.Contains("https", StringComparison.OrdinalIgnoreCase))
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

// Only redirect to HTTPS when the app actually has an HTTPS listener configured.
// On AKS the pod runs as Production but speaks plain HTTP (TLS terminates at the
// ingress), with no ASPNETCORE_URLS https binding — so UseHttpsRedirection would
// only emit the "Failed to determine the https port for redirect" warning and can
// interfere with requests. Guard on the configured URLs rather than environment.
var aspnetcoreUrls = builder.Configuration["ASPNETCORE_URLS"];
if (!string.IsNullOrEmpty(aspnetcoreUrls) && aspnetcoreUrls.Contains("https", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

// BUG-001: Decimal model binding is handled by InvariantDecimalModelBinderProvider
// (registered in AddControllersWithViews above). UseRequestLocalization is NOT
// used because DecimalModelBinder resolves its culture from
// CultureInfo.CurrentCulture (process culture), not from request-localization
// middleware — so the middleware was ineffective on en_ZA hosts.

app.UseRouting();

app.UseRateLimiter();               // must run between UseRouting and UseAuthentication

// Defense-in-depth (SEC/DOC-01): never serve anything under /_uploads via static files.
// Generated letter/blob artefacts now live outside wwwroot (in letter-blobs/) and are served
// only by scope-checked controller actions (FileMasterController.LetterPdf).
// This block remains here as a safety net in case any stray file appears under wwwroot/_uploads.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

// Serve wwwroot static files (CSS, JS, fonts, images) BEFORE the auth pipeline runs.
// MapStaticAssets() registers fingerprinted/compressed endpoint handlers but operates
// after UseAuthorization — meaning unauthenticated requests (e.g. the login page
// requesting site.css) hit the auth middleware first and get a 401 redirect instead
// of the file. UseStaticFiles() short-circuits before auth and fixes this.
// MapStaticAssets() stays below for the fingerprinted asset support it adds to MVC routes.
app.UseStaticFiles();

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
