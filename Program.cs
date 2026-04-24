using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
// TODO(Phase 4): builder.Services.AddScoped<IScopedCaseQuery, ScopedCaseQuery>();

// Seeders
builder.Services.AddScoped<SeedDataService>();
// TODO(Phase 5): builder.Services.AddScoped<IdentitySeeder>();

var app = builder.Build();

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    await db.Database.MigrateAsync();

    var refSeeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await refSeeder.SeedAsync();

    // TODO(Phase 5): var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    // TODO(Phase 5): await identitySeeder.SeedAsync();
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
