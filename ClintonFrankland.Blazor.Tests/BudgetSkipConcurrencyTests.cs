using System.Data.Common;
using System.Reflection;
using Bunit;
using ClintonFrankland.Components;
using ClintonFrankland.Components.Pages;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Attachments;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Radzen;
using Radzen.Blazor;
using BudgetPage = ClintonFrankland.Components.Pages.Budget;
using BudgetEntity = ClintonFrankland.Models.Entities.Budget;

namespace ClintonFrankland.Blazor.Tests;

public sealed class BudgetSkipConcurrencyTests : BunitContext
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OverlappingSkipCallbacksStayBusyUntilCompletionAndReleaseAfterFailure(bool checkbook, bool failFirst)
    {
        var barrier = new BudgetQueryBarrier { FailFirst = failFirst };
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var database = ConfigureServices(connection, barrier);
        var page = RenderPage(checkbook);
        var action = page.FindComponents<BudgetItemActions>().First();
        var originalSkip = action.Instance.OnSkip;
        barrier.Armed = true;

        Task firstSkip = Task.CompletedTask;
        await page.InvokeAsync(() => { firstSkip = originalSkip.InvokeAsync(); });
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            page.WaitForAssertion(() =>
            {
                Assert.True(action.Instance.IsInProgress);
                Assert.Equal("true", action.Find(".budget-item-actions").GetAttribute("aria-busy"));
                Assert.All(action.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
            });

            // Invoke the page callback directly: a disabled button alone must not hide a missing page guard.
            await page.InvokeAsync(() => originalSkip.InvokeAsync());
            Assert.Equal(1, barrier.BudgetQueries);
        }
        finally
        {
            barrier.Release.TrySetResult();
        }
        await firstSkip.WaitAsync(TimeSpan.FromSeconds(5));

        if (failFirst)
        {
            page.WaitForAssertion(() =>
            {
                Assert.Contains("Simulated skip failure", page.Markup);
                Assert.False(action.Instance.IsInProgress);
                Assert.False(action.Find(".budget-item-more").HasAttribute("disabled"));
            });
            await page.InvokeAsync(() => originalSkip.InvokeAsync());
        }

        // The selected occurrence advances exactly once, creates no transaction, and remains stale on retry.
        Assert.Equal(DateTime.Today.AddMonths(1), (await database.Budgets.SingleAsync()).NextDueDate);
        Assert.Empty(await database.Transactions.ToListAsync());
        await page.InvokeAsync(() => originalSkip.InvokeAsync());
        Assert.Equal(DateTime.Today.AddMonths(1), (await database.Budgets.SingleAsync()).NextDueDate);
        Assert.All(page.FindComponents<BudgetItemActions>(), row => Assert.False(row.Instance.IsInProgress));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlySkipCallbackDoesNotEnterFinancialService(bool checkbook)
    {
        var barrier = new BudgetQueryBarrier();
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var database = ConfigureServices(connection, barrier);
        var page = RenderPage(checkbook);
        barrier.Armed = true;
        var method = page.Instance.GetType().GetMethod(checkbook ? "SkipBudgetAsync" : "SkipOccurrenceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await page.InvokeAsync(() => (Task)method.Invoke(page.Instance,
            [new BudgetItemViewModel { BudgetId = 101, DueDate = DateTime.Today, CanManageFinancialData = false }])!);

        Assert.Equal(0, barrier.BudgetQueries);
        Assert.Equal(DateTime.Today, database.Budgets.Local.Single().NextDueDate);
        Assert.All(page.FindComponents<BudgetItemActions>(), row => Assert.False(row.Instance.IsInProgress));
    }

    private IRenderedComponent<ComponentBase> RenderPage(bool checkbook)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(checkbook ? "/checkbook" : "/budget");
        IRenderedComponent<ComponentBase> page = checkbook ? Render<Checkbook>() : Render<BudgetPage>();
        if (checkbook)
        {
            page.WaitForAssertion(() => Assert.Contains("Budgets (", page.Markup));
            page.Find("[title='Show Budget Items']").Click();
        }
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindComponents<BudgetItemActions>()));
        return page;
    }

    private ClintonFranklandDbContext ConfigureServices(SqliteConnection connection, BudgetQueryBarrier barrier)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.AddStub<RadzenChart>();
        Services.AddRadzenComponents();
        var database = new ClintonFranklandDbContext(new DbContextOptionsBuilder<ClintonFranklandDbContext>()
            .UseSqlite(connection).AddInterceptors(barrier).Options);
        database.Database.EnsureCreated();
        database.Users.Add(new User { UserId = 7, UserName = "tester", DisplayName = "Tester" });
        database.SharedBudgets.Add(new SharedBudget { SharedBudgetId = 50, Name = "Household", OwnerUserId = 7 });
        database.BudgetMembers.Add(new BudgetMember { SharedBudgetId = 50, UserId = 7, Role = BudgetMemberRole.Editor });
        database.Categories.Add(new Category { CategoryId = 10, CategoryName = "Housing", UserId = 7, SharedBudgetId = 50 });
        database.Frequencies.Add(new Frequency { FrequencyId = 4, FrequencyName = "Monthly", Sort = 1 });
        database.Budgets.Add(new BudgetEntity { BudgetId = 101, BudgetName = "Rent", BudgetTypeId = 1,
            CategoryId = 10, FrequencyId = 4, UserId = 7, SharedBudgetId = 50, NextDueDate = DateTime.Today, Amount = 25m });
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

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BudgetSkipConcurrencyTests";
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class BudgetQueryBarrier : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public bool FailFirst { get; init; }
        public int BudgetQueries { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Armed || !command.CommandText.Contains("FROM \"cfBudgets\"") || !command.CommandText.Contains("LIMIT 1"))
                return result;

            BudgetQueries++;
            if (BudgetQueries == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                if (FailFirst)
                    throw new InvalidOperationException("Simulated skip failure");
            }
            return result;
        }
    }
}
