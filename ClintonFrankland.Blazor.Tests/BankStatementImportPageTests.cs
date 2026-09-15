using System.Reflection;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Text;
using Bunit;
using ClintonFrankland.Components.Pages;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Attachments;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Blazor.Tests;

public sealed class BankStatementImportPageTests : BunitContext
{
    [Fact]
    public async Task QifPreviewShowsDestinationAndClearedStateThenImportsSignedRows()
    {
        var database = ConfigureServices();
        var page = await OpenImportAsync();
        await SelectAsync(page, "bank.qif", "!Type:Bank\nD09/15/2026\nT-12.50\nPCorner Market\nLFood\nCX\nNreference-1\n^\n");
        Assert.Contains("Household Checking", page.Markup);
        Assert.Contains("duplicate transactions", page.Markup);
        Assert.Contains("reference-1", page.Markup);
        var row = Assert.Single(page.FindComponent<RadzenDataGrid<BankStatementRow>>().Instance.Data!);
        Assert.True(row.Cleared);
        Assert.Equal(-12.50m, row.Amount);
        Assert.Empty(database.Transactions);

        await ImportButton(page).Find("button").ClickAsync(new());
        var saved = Assert.Single(database.Transactions);
        Assert.Equal(-12.50m, saved.Amount);
        Assert.True(saved.Cleared);
        Assert.Equal(20, saved.AccountId);
        Assert.Equal(50, saved.SharedBudgetId);
        Assert.DoesNotContain("Statement preview", page.Markup);
    }

    [Fact]
    public async Task InvalidReplacementClearsPriorStatementAndDisablesImport()
    {
        var database = ConfigureServices();
        var page = await OpenImportAsync();
        await SelectAsync(page, "bank.qif", "!Type:Bank\nD09/15/2026\nT-12.50\nPOld selection\n^\n");
        Assert.False(ImportButton(page).Instance.Disabled);
        await SelectAsync(page, "broken.ofx", "not a bank statement");
        Assert.True(ImportButton(page).Instance.Disabled);
        Assert.DoesNotContain("Old selection", page.Markup);
        Assert.Contains("Could not read the statement", page.Markup);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task CsvMappingStillImportsAndKeepsEntriesUncleared()
    {
        var database = ConfigureServices();
        var page = await OpenImportAsync();
        await SelectAsync(page, "bank.csv", "Date,Amount,Payee,Category\n09/15/2026,24.75,Refund,Food\n");
        Assert.Equal(4, page.FindComponents<RadzenDropDown<string>>().Count);
        Assert.False(Assert.Single(page.FindComponent<RadzenDataGrid<BankStatementRow>>().Instance.Data!).Cleared);
        await ImportButton(page).Find("button").ClickAsync(new());
        var saved = Assert.Single(database.Transactions);
        Assert.Equal(24.75m, saved.Amount);
        Assert.False(saved.Cleared);
    }

    [Fact]
    public async Task ValidationIncludesRowsBeyondVisiblePreviewAndSavesNothing()
    {
        var database = ConfigureServices();
        var page = await OpenImportAsync();
        var contents = "Date,Amount,Payee\n" + string.Concat(Enumerable.Repeat("09/15/2026,-1.00,Valid\n", 10)) + "bad-date,-2.00,Invalid\n";
        await SelectAsync(page, "bank.csv", contents);
        Assert.Equal(10, page.FindComponent<RadzenDataGrid<BankStatementRow>>().Instance.Data!.Count());
        await ImportButton(page).Find("button").ClickAsync(new());
        Assert.Contains("1 row(s)", page.Markup);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task AccountChangedAfterPreviewRejectsImportWithoutSaving()
    {
        var database = ConfigureServices();
        var page = await OpenImportAsync();
        await SelectAsync(page, "bank.csv", "Date,Amount,Payee\n09/15/2026,-1.00,Example\n");
        database.Accounts.Single(account => account.AccountId == 20).IsDefault = false;
        database.Accounts.Add(new Account { AccountId = 21, AccountName = "Changed default", AccountTypeId = 1,
            UserId = 7, SharedBudgetId = 50, IsDefault = true });
        await database.SaveChangesAsync();
        await ImportButton(page).Find("button").ClickAsync(new());
        Assert.Contains("Could not import transactions", page.Markup);
        Assert.Empty(database.Transactions);
    }

    [Fact]
    public async Task LaterSelectionWinsWhenEarlierFileReadCompletesLast()
    {
        ConfigureServices();
        var page = await OpenImportAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldFile = new DelayedStatementFile(gate.Task);
        Task oldRead = Task.CompletedTask;
        await page.InvokeAsync(() =>
        {
            oldRead = page.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([oldFile]));
        });
        try
        {
            await SelectAsync(page, "new.csv", "Date,Amount,Payee\n09/15/2026,-2.00,Newest selection\n");
        }
        finally
        {
            gate.TrySetResult();
        }
        await oldRead.WaitAsync(TimeSpan.FromSeconds(5));
        var row = Assert.Single(page.FindComponent<RadzenDataGrid<BankStatementRow>>().Instance.Data!);
        Assert.Equal("Newest selection", row.Payee);
        Assert.DoesNotContain("Old delayed selection", page.Markup);
    }

    [Fact]
    public async Task OpeningDestinationPreventsOverlappingOpenAndCancelCallbacks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var gate = new DestinationQueryGate();
        ConfigureServices(new DbContextOptionsBuilder<ClintonFranklandDbContext>().UseSqlite(connection).AddInterceptors(gate).Options);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/checkbook");
        var page = Render<Checkbook>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[title='Import CSV, OFX, QFX, or QIF transactions']")));
        gate.Armed = true;
        Task opening = Task.CompletedTask;
        await page.InvokeAsync(() => { opening = page.Find("[title='Import CSV, OFX, QFX, or QIF transactions']").ClickAsync(new()); });
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            page.WaitForAssertion(() => Assert.Contains("Loading destination account", page.Markup));
            Assert.True(page.FindComponents<RadzenButton>().Single(button => button.Instance.Text == "Cancel").Instance.Disabled);
            var cancel = typeof(Checkbook).GetMethod("CancelImport", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var show = typeof(Checkbook).GetMethod("ShowImportTransactions", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await page.InvokeAsync(() => cancel.Invoke(page.Instance, null));
            await page.InvokeAsync(() => (Task)show.Invoke(page.Instance, null)!);
            Assert.Equal(1, gate.Calls);
            Assert.Contains("Loading destination account", page.Markup);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Destination account:", page.Markup);
        Assert.False(page.FindComponents<RadzenButton>().Single(button => button.Instance.Text == "Cancel").Instance.Disabled);
    }

    private async Task<IRenderedComponent<Checkbook>> OpenImportAsync()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/checkbook");
        var page = Render<Checkbook>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[title='Import CSV, OFX, QFX, or QIF transactions']")));
        await page.Find("[title='Import CSV, OFX, QFX, or QIF transactions']").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Contains("Destination account:", page.Markup));
        return page;
    }

    private static IRenderedComponent<RadzenButton> ImportButton(IRenderedComponent<Checkbook> page) =>
        page.FindComponents<RadzenButton>().Single(button => button.Instance.Icon == "upload");

    private static Task SelectAsync(IRenderedComponent<Checkbook> page, string name, string contents) =>
        page.InvokeAsync(() => page.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(
            new InputFileChangeEventArgs([new StatementFile(name, contents)])));

    private ClintonFranklandDbContext ConfigureServices(DbContextOptions<ClintonFranklandDbContext>? options = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.AddStub<RadzenChart>();
        Services.AddRadzenComponents();
        var database = new ClintonFranklandDbContext(options ?? new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        database.Database.EnsureCreated();
        database.Users.Add(new User { UserId = 7, UserName = "tester", DisplayName = "Tester", ActiveSharedBudgetId = 50 });
        database.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 50, Name = "Household", OwnerUserId = 7 });
        database.BudgetMembers.Add(new BudgetMember { SharedBudgetId = 50, UserId = 7, Role = BudgetMemberRole.Owner });
        database.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Checking" });
        database.Accounts.Add(new Account { AccountId = 20, AccountName = "Household Checking", AccountTypeId = 1,
            UserId = 7, SharedBudgetId = 50, IsDefault = true });
        database.SaveChanges();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["AppSettings:DefaultUserId"] = "7" }).Build();
        var authentication = new AuthService(configuration,
            new ProtectedSessionStorage(JSInterop.JSRuntime, new EphemeralDataProtectionProvider()),
            database, new Microsoft.AspNetCore.Http.HttpContextAccessor());
        typeof(AuthService).GetField("_currentUser", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(authentication, new UserInfo { UserId = 7, UserName = "tester", DisplayName = "Tester", IsLoggedIn = true });
        typeof(AuthService).GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(authentication, true);
        Services.AddSingleton(database);
        Services.AddSingleton(authentication);
        Services.AddSingleton(new SiteInfoService(configuration));
        Services.AddSingleton<CurrentUserContext>();
        Services.AddSingleton<SharedBudgetDataService>();
        Services.AddSingleton<BudgetDataService>();
        Services.AddSingleton<BudgetItemsDataService>();
        Services.AddSingleton<BudgetScheduleService>();
        Services.AddSingleton<CheckbookDataService>();
        Services.AddSingleton<BudgetAllowanceService>();
        Services.AddSingleton<BalanceSummaryService>();
        Services.AddSingleton<TransactionRulesDataService>();
        Services.AddSingleton<TransactionCsvService>();
        Services.AddSingleton<BankStatementImportService>();
        Services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
        Services.AddSingleton<IAttachmentMalwareScanner, NoOpAttachmentMalwareScanner>();
        Services.Configure<ReceiptAttachmentOptions>(_ => { });
        Services.AddSingleton<ReceiptAttachmentStorageService>();
        Services.AddSingleton<DialogService>();
        return database;
    }

    private sealed class StatementFile(string name, string contents) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => Encoding.UTF8.GetByteCount(contents);
        public string ContentType => "application/octet-stream";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Size > maxAllowedSize ? throw new IOException("File is too large.") : new MemoryStream(Encoding.UTF8.GetBytes(contents));
    }

    private sealed class DelayedStatementFile(Task release) : IBrowserFile
    {
        public string Name => "old.csv";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 100;
        public string ContentType => "text/csv";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new DelayedStream(release);
    }

    private sealed class DelayedStream(Task release) : MemoryStream(Encoding.UTF8.GetBytes("Date,Amount,Payee\n09/15/2026,-1.00,Old delayed selection\n"))
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await release.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class DestinationQueryGate : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public int Calls { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("FROM \"cfUsers\"", StringComparison.Ordinal))
            {
                Calls++;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BankStatementImportPageTests";
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
