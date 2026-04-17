using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDBContext>(
    options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Repository DI registrations
builder.Services.AddScoped<IPropertyInterface, PropertyRepository>();
builder.Services.AddScoped<IAddress, AddressRepository>();
builder.Services.AddScoped<IFileMaster, FileMasterRepository>();
builder.Services.AddScoped<IForestation, ForestationRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();

// Services
builder.Services.AddScoped<SeedDataService>();

var app = builder.Build();

// Seed reference data on startup
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
