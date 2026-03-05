using ClintonFrankland.Data;
using ClintonFrankland.Services;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Entity Framework Core DbContext
builder.Services.AddDbContext<ClintonFranklandDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    options.UseSqlServer(connectionString);
});

// Register application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SiteInfoService>();
builder.Services.AddRadzenComponents();

var app = builder.Build();

// Ensure currency formatting uses US dollars across server-rendered UI ("$" vs generic "¤").
var usCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = usCulture;
CultureInfo.DefaultThreadCurrentUICulture = usCulture;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ClintonFrankland.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
