using ClintonFrankland.Data;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Globalization;

// GLOBALIZATION STRATEGY:
// The app intentionally pins culture to en-US (United States - English) for all server-side rendering and formatting.
// Rationale: The primary driver is currency formatting. en-US uses the "$" symbol (e.g., "$1,234.56"),
// while most other cultures use a generic currency symbol like "¤" (e.g., "¤1,234.56" or "1 234,56¤").
// This ensures consistent, predictable currency display across all environments (dev, prod, review).
// See GLOBALIZATION.md for detailed rationale and implementation details.

// Pin the default culture early (before the app builds) so server-side currency formatting
// uses "$" instead of the generic currency symbol "¤".
var usCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = usCulture;
CultureInfo.DefaultThreadCurrentUICulture = usCulture;
CultureInfo.CurrentCulture = usCulture;
CultureInfo.CurrentUICulture = usCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddHostedService<BillDueNotificationWorker>();
builder.Services.AddScoped<AccountsDataService>();
builder.Services.AddScoped<BudgetItemsDataService>();
builder.Services.AddScoped<BudgetDataService>();
builder.Services.AddScoped<CheckbookDataService>();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddRadzenComponents();

var app = builder.Build();

// Apply EF Core migrations on startup.
// Baseline: the app predates migrations, but we use an empty baseline migration to seed __EFMigrationsHistory.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();

    app.Logger.LogInformation("Applying database migrations (startup)");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations complete");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Database migration failed");
    throw;
}

// Ensure required user preference columns exist for legacy databases.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();
    try
    {
        await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('cfUsers', 'ListButtonsRight') IS NULL ALTER TABLE cfUsers ADD ListButtonsRight BIT NOT NULL CONSTRAINT DF_cfUsers_ListButtonsRight DEFAULT(1)");
        await db.Database.ExecuteSqlRawAsync(@"IF OBJECT_ID('cfAuthLoginAudit', 'U') IS NULL
BEGIN
    CREATE TABLE cfAuthLoginAudit (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AttemptedAtUtc DATETIME2 NOT NULL,
        UserName NVARCHAR(128) NULL,
        ClientIp NVARCHAR(64) NULL,
        Succeeded BIT NOT NULL,
        Reason NVARCHAR(256) NULL
    );
    CREATE INDEX IX_cfAuthLoginAudit_AttemptedAtUtc ON cfAuthLoginAudit(AttemptedAtUtc);
    CREATE INDEX IX_cfAuthLoginAudit_UserName ON cfAuthLoginAudit(UserName);
END");

        // cfSmtpSettings is managed via EF migrations.
    }
    catch
    {
        // Best effort; ignore if already exists or DB lacks permissions for metadata check.
    }
}

// Force request/circuit culture to en-US for consistent formatting in Blazor Server.
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US");
app.UseRequestLocalization(localizationOptions);

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
