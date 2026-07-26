using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Attachments;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Radzen;
using System.Globalization;
using System.Security.Claims;

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
var authentikOidcOptions = builder.Configuration
    .GetSection(AuthentikOidcOptions.SectionName)
    .Get<AuthentikOidcOptions>() ?? new AuthentikOidcOptions();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(usCulture.Name)
        .AddSupportedCultures(usCulture.Name)
        .AddSupportedUICultures(usCulture.Name);
});
builder.Services.Configure<AuthentikOidcOptions>(builder.Configuration.GetSection(AuthentikOidcOptions.SectionName));
builder.Services.Configure<PlaidOptions>(builder.Configuration.GetSection(PlaidOptions.SectionName));
builder.Services.AddHttpClient<IPlaidClient, PlaidClient>(client => client.BaseAddress = new Uri("https://sandbox.plaid.com/"));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("0.0.0.0/0"));
});

if (authentikOidcOptions.IsUsable)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = AuthentikOidcDefaults.CookieScheme;
            options.DefaultChallengeScheme = AuthentikOidcDefaults.OpenIdConnectScheme;
        })
        .AddCookie(AuthentikOidcDefaults.CookieScheme, options =>
        {
            options.Cookie.Name = authentikOidcOptions.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
        })
        .AddOpenIdConnect(AuthentikOidcDefaults.OpenIdConnectScheme, options =>
        {
            options.Authority = authentikOidcOptions.Authority;
            options.ClientId = authentikOidcOptions.ClientId;
            options.ClientSecret = authentikOidcOptions.ClientSecret;
            options.CallbackPath = authentikOidcOptions.CallbackPath;
            options.SignedOutCallbackPath = authentikOidcOptions.SignedOutCallbackPath;
            options.ResponseType = "code";
            options.ResponseMode = "query";
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.ClaimActions.MapJsonKey("groups", "groups");
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "groups";
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnTokenValidated = async context =>
            {
                var settings = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<AuthentikOidcOptions>>()
                    .Value;
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ClintonFrankland.AuthentikOidc");
                var receivedGroups = AuthentikOidcClaims.GetGroups(context.Principal!);
                var requiredGroups = settings.AllowedGroups
                    .Where(group => !string.IsNullOrWhiteSpace(group))
                    .Select(group => group.Trim())
                    .ToArray();

                if (!AuthentikOidcClaims.IsInAllowedGroup(context.Principal!, settings.AllowedGroups))
                {
                    AuthentikOidcDiagnostics.LogMissingRequiredGroup(logger, requiredGroups, receivedGroups);
                    context.Fail("Authentik user is not in an allowed Budget App group.");
                    return;
                }

                var profile = AuthentikOidcClaims.BuildExternalProfile(context.Principal!, settings.NormalizedProviderName);
                if (profile is null)
                {
                    AuthentikOidcDiagnostics.LogMissingStableSubject(logger, settings.NormalizedProviderName);
                    context.Fail("Authentik login did not include a stable subject claim.");
                    return;
                }

                var isLinkIntent = string.Equals(
                    context.Properties?.RedirectUri,
                    AuthentikOidcDefaults.LinkConfirmationPath,
                    StringComparison.Ordinal);
                if (isLinkIntent)
                {
                    context.Principal = AuthentikOidcClaims.CreateLinkIntentPrincipal(profile, receivedGroups);
                    AuthentikOidcDiagnostics.LogLinkIntentSuccess(logger, profile, receivedGroups);
                    return;
                }

                var linker = context.HttpContext.RequestServices.GetRequiredService<ExternalIdentityLinkService>();
                var user = await linker.RecordExternalLoginAsync(profile);
                if (user is null)
                {
                    AuthentikOidcDiagnostics.LogUnknownLinkedUser(logger, profile);
                    context.Fail("Authentik user is not linked to an active Budget App user.");
                    return;
                }

                context.Principal = AuthentikOidcClaims.CreateBudgetUserPrincipal(user, receivedGroups);
                AuthentikOidcDiagnostics.LogLinkedUserSuccess(logger, profile, user.UserId, receivedGroups);
            };
            options.Events.OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ClintonFrankland.AuthentikOidc");
                AuthentikOidcDiagnostics.LogRemoteFailure(logger, context.Failure);
                var message = Uri.EscapeDataString(context.Failure?.Message ?? "Authentik sign-in failed.");
                context.Response.Redirect($"/login?externalError={message}");
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

    builder.Services.AddAuthorization();
}
else
{
    // Endpoint authorization remains registered even when this deployment uses the legacy session login.
    builder.Services.AddAuthorization();
}

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
builder.Services.AddScoped<ExternalIdentityLinkService>();
builder.Services.AddScoped<SiteInfoService>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddScoped<IEmailSender, EmailSenderService>();
builder.Services.AddScoped<WeeklyUpcomingBillsDigestService>();
builder.Services.AddScoped<MigrationErrorTracker>();
builder.Services.AddHostedService<BillDueNotificationWorker>();

// Backups (optional, disabled by default)
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddHostedService<DatabaseBackupWorker>();

builder.Services.AddScoped<AccountsDataService>();
builder.Services.AddScoped<BudgetItemsDataService>();
builder.Services.AddScoped<BudgetItemsExportService>();
builder.Services.AddScoped<PayeesDataService>();
builder.Services.AddScoped<SharedBudgetDataService>();
builder.Services.AddScoped<BudgetInviteService>();
builder.Services.AddScoped<BudgetScheduleService>();
builder.Services.AddScoped<BudgetAllowanceService>();
builder.Services.AddScoped<BudgetDataService>();
builder.Services.AddScoped<CategoryBudgetDataService>();
builder.Services.AddScoped<CheckbookDataService>();
builder.Services.AddScoped<TransactionCsvService>();
builder.Services.AddScoped<TransactionRulesDataService>();
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<InsightsDataService>();
builder.Services.AddScoped<ReportsDataService>();
builder.Services.AddScoped<DashboardApiAuthService>();
builder.Services.AddScoped<PlaidConnectionService>();
builder.Services.Configure<ReceiptAttachmentOptions>(builder.Configuration.GetSection(ReceiptAttachmentOptions.SectionName));
builder.Services.Configure<CategoryBudgetAlertOptions>(builder.Configuration.GetSection(CategoryBudgetAlertOptions.SectionName));
builder.Services.AddScoped<IAttachmentMalwareScanner, NoOpAttachmentMalwareScanner>();
builder.Services.AddScoped<ReceiptAttachmentStorageService>();
builder.Services.AddHostedService<ReceiptAttachmentCleanupWorker>();
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
            try
            {
                await db.Database.MigrateAsync();
                applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
                startupDiagnostics.LastAppliedMigration = applied.LastOrDefault();
            }
            catch (Exception ex)
            {
                var tracker = scope.ServiceProvider.GetRequiredService<MigrationErrorTracker>();
                var targetMigration = pending.LastOrDefault();
                await tracker.CaptureMigrationErrorAsync(ex, targetMigration);
                throw;
            }
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
app.UseForwardedHeaders();
app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
if (authentikOidcOptions.IsUsable)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseAntiforgery();

app.MapGet("/auth/authentik/login", (IOptions<AuthentikOidcOptions> options, string? returnUrl) =>
    AuthentikOidcEndpoints.ChallengeLogin(options.Value, returnUrl));

app.MapGet("/auth/authentik/link", (IOptions<AuthentikOidcOptions> options) =>
    AuthentikOidcEndpoints.ChallengeLink(options.Value));

app.MapGet("/auth/logout", async (HttpContext context, string? returnUrl) =>
{
    if (authentikOidcOptions.IsUsable && context.User.Identity?.IsAuthenticated == true)
        await context.SignOutAsync(AuthentikOidcDefaults.CookieScheme);

    return AuthentikOidcEndpoints.LocalLogout(returnUrl);
});

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

app.MapPost("/api/plaid/link-token", async (HttpContext context, PlaidConnectionService plaid, CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedBudgetUserId(context, out var userId)) return Results.Unauthorized();
    var token = await plaid.CreateLinkTokenAsync(userId, cancellationToken);
    return Results.Ok(new { linkToken = token.Token, expiration = token.Expiration });
}).RequireAuthorization();

app.MapPost("/api/plaid/items/{plaidItemId:int}/update-link-token", async (HttpContext context, int plaidItemId, PlaidConnectionService plaid, CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedBudgetUserId(context, out var userId)) return Results.Unauthorized();
    var token = await plaid.CreateUpdateLinkTokenAsync(userId, plaidItemId, cancellationToken);
    return Results.Ok(new { linkToken = token.Token, expiration = token.Expiration });
}).RequireAuthorization();

app.MapPost("/api/plaid/exchange", async (HttpContext context, PlaidExchangeRequest request, PlaidConnectionService plaid, CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedBudgetUserId(context, out var userId)) return Results.Unauthorized();
    var result = await plaid.ExchangePublicTokenAsync(userId, request.PublicToken, request.InstitutionId, request.InstitutionName, cancellationToken);
    return Results.Ok(new { result.PlaidItemId, result.Accounts });
}).RequireAuthorization();

app.MapPut("/api/plaid/items/{plaidItemId:int}/mappings/{plaidAccountId}", async (HttpContext context, int plaidItemId, string plaidAccountId, PlaidMappingRequest request, PlaidConnectionService plaid, CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedBudgetUserId(context, out var userId)) return Results.Unauthorized();
    await plaid.MapAccountAsync(userId, plaidItemId, plaidAccountId, request.BudgetAccountId, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();

app.MapDelete("/api/plaid/items/{plaidItemId:int}", async (HttpContext context, int plaidItemId, PlaidConnectionService plaid, CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedBudgetUserId(context, out var userId)) return Results.Unauthorized();
    await plaid.DisconnectAsync(userId, plaidItemId, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();

app.MapRazorComponents<ClintonFrankland.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool TryGetAuthenticatedBudgetUserId(HttpContext context, out int userId)
{
    userId = 0;
    return context.User.Identity?.IsAuthenticated == true &&
        int.TryParse(context.User.FindFirst(AuthentikOidcDefaults.BudgetUserIdClaim)?.Value, out userId) && userId > 0;
}

sealed record PlaidExchangeRequest(string PublicToken, string? InstitutionId, string? InstitutionName);
sealed record PlaidMappingRequest(int BudgetAccountId);
