using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

// QuestPDF licence — Community for the demo build. See docs/superpowers/specs/2026-04-24-mvp-hardening-design.md §4.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

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

// Microsoft (Entra ID) sign-in via the OpenID Connect external-login provider.
// This integrates with ASP.NET Identity's existing cookie session: after successful
// OIDC sign-in, AccountController.ExternalLoginCallback uses SignInManager to
// link/create the local ApplicationUser and creates the standard Identity cookie.
// Tenant + client + secret come from configuration (App Service settings or user-secrets).
var entraTenantId = builder.Configuration["AzureAd:TenantId"];
var entraClientId = builder.Configuration["AzureAd:ClientId"];
var entraClientSecret = builder.Configuration["AzureAd:ClientSecret"];
if (!string.IsNullOrEmpty(entraTenantId) && !string.IsNullOrEmpty(entraClientId) && !string.IsNullOrEmpty(entraClientSecret))
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("Microsoft", "Microsoft", options =>
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

// Authorisation policies
builder.Services.AddAuthorization(DwsPolicies.Configure);

// Repository DI
builder.Services.AddScoped<IPropertyInterface, PropertyRepository>();
builder.Services.AddScoped<IAddress, AddressRepository>();
builder.Services.AddScoped<IFileMaster, FileMasterRepository>();
builder.Services.AddScoped<IForestation, ForestationRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IScopedCaseQuery, ScopedCaseQuery>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Workflow transition guards — evaluated in registration order by WorkflowService.MoveToStateAsync.
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp2SpatialInfoGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp3WarmsReviewedGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp4AdditionalInfoGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp5MapbookPresentGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp8DamOrNAGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp9SfraOrNAGuard>();

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

// Seeders
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<IdentitySeeder>();

var app = builder.Build();

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose the Program class for Microsoft.AspNetCore.Mvc.Testing
public partial class Program { }
