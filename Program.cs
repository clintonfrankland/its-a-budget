using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Globalization;

static string? GetArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

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
builder.Services.AddSingleton<StartupDiagnosticsState>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SiteInfoService>();
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddHostedService<BillDueNotificationWorker>();

// Backups (optional, disabled by default)
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddHostedService<DatabaseBackupWorker>();

builder.Services.AddScoped<AccountsDataService>();
builder.Services.AddScoped<BudgetItemsDataService>();
builder.Services.AddScoped<BudgetDataService>();
builder.Services.AddScoped<CheckbookDataService>();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<DashboardApiAuthService>();
builder.Services.AddRadzenComponents();

// CLI-style commands (backup/export + restore smoke test)
// Usage examples:
//   dotnet run -- backup --out ./backups
//   dotnet run -- restore-smoketest --file ./backups/Db_backup_20260310_010203.bak
if (args.Length > 0)
{
    var cmd = args[0].Trim().ToLowerInvariant();
    if (cmd is "backup" or "export")
    {
        var outDir = GetArgValue(args, "--out") ?? GetArgValue(args, "-o") ?? "./backups";

        using var tempApp = builder.Build();
        var svc = tempApp.Services.GetRequiredService<DatabaseBackupService>();
        await svc.BackupDatabaseAsync(connectionString: null, outputDirectory: outDir, ct: CancellationToken.None);
        return;
    }

    if (cmd is "restore-smoketest" or "restore")
    {
        var file = GetArgValue(args, "--file") ?? GetArgValue(args, "-f")
            ?? throw new ArgumentException("Missing required --file <path-to-bak>");

        using var tempApp = builder.Build();
        var svc = tempApp.Services.GetRequiredService<DatabaseBackupService>();
        await svc.RestoreSmokeTestAsync(connectionString: null, backupFile: file, ct: CancellationToken.None);
        return;
    }
}

var app = builder.Build();

// Apply EF Core migrations on startup and collect diagnostics for admin banner.
var startupDiagnostics = app.Services.GetRequiredService<StartupDiagnosticsState>();
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ClintonFranklandDbContext>();

    app.Logger.LogInformation("Checking database connectivity/migrations (startup)");

    var canConnect = await db.Database.CanConnectAsync();
    if (!canConnect)
    {
        startupDiagnostics.Checked = true;
        startupDiagnostics.HasIssue = true;
        startupDiagnostics.FailureType = "connectivity";
        startupDiagnostics.ErrorMessage = "Unable to connect to configured SQL Server database.";
        app.Logger.LogCritical("Database connectivity check failed");
    }
    else
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        startupDiagnostics.LastAppliedMigration = applied.LastOrDefault();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            app.Logger.LogInformation("Applying {Count} pending migration(s)", pending.Count);
            await db.Database.MigrateAsync();
            applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            startupDiagnostics.LastAppliedMigration = applied.LastOrDefault();
        }

        startupDiagnostics.Checked = true;
        startupDiagnostics.HasIssue = false;
        app.Logger.LogInformation("Database startup checks complete");
    }
}
catch (Exception ex)
{
    startupDiagnostics.Checked = true;
    startupDiagnostics.HasIssue = true;
    startupDiagnostics.FailureType = startupDiagnostics.FailureType ?? "migration";
    startupDiagnostics.ErrorMessage = ex.Message;

    app.Logger.LogCritical(ex, "Database startup check/migration failed");
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

app.MapPost("/api/home-dashboard-summary", async (
    HomeDashboardSummaryAuthRequest request,
    DashboardApiAuthService auth,
    DashboardDataService dashboardData) =>
{
    var userId = await auth.ValidateAndResolveUserIdAsync(request.Username, request.Password);
    if (!userId.HasValue)
        return Results.Unauthorized();

    var today = DateTime.Today;
    var snapshot = await dashboardData.GetSnapshotAsync(userId.Value, today);

    var dueThrough = today.AddDays(7);
    var upcomingBills = snapshot.UpcomingBills
        .Where(b => b.DueDate.Date >= today && b.DueDate.Date <= dueThrough)
        .OrderBy(b => b.DueDate)
        .ThenBy(b => b.Name)
        .ToList();

    var response = new HomeDashboardSummaryResponse
    {
        AsOfDate = snapshot.AsOfDate,
        TodayBalance = snapshot.TodayBalance,
        UpcomingBillsTotal = upcomingBills.Sum(b => b.Amount),
        SafeToSpend = snapshot.LowestProjectedBalance,
        SafeToSpendDate = snapshot.LowestProjectedBalanceDate,
        UpcomingBills = upcomingBills,
        CategorySpend = snapshot.CategorySpend
    };

    return Results.Ok(response);
})
.DisableAntiforgery();

app.MapRazorComponents<ClintonFrankland.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
