using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Reports
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUser { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ReportsDataService ReportsData { get; set; } = default!;

    private DateOnly selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private int horizonDays = 30;
    private bool loading = true;
    private List<SpendPlanRow> spendPlan = [];
    private List<CategoryTrendRow> trends = [];
    private List<InsightsMonth> months = [];
    private CashflowReport? cashflow;
    private NetWorthReport? netWorth;
    private string spendError = string.Empty, trendError = string.Empty, cashflowError = string.Empty, netWorthError = string.Empty;

    private string SelectedMonthInput => selectedMonth.ToString("yyyy-MM");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/reports")}");
            return;
        }
        await LoadAllAsync();
        StateHasChanged();
    }

    private async Task OnMonthChanged(ChangeEventArgs args)
    {
        if (!DateOnly.TryParseExact($"{args.Value}-01", "yyyy-MM-dd", out var parsed)) return;
        selectedMonth = parsed;
        loading = true;
        await LoadHistoricalAsync();
        loading = false;
    }

    private async Task OnHorizonChanged(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), out var value) || value < 30) return;
        horizonDays = value;
        await LoadCashflowAsync(CurrentUser.UserId);
    }

    private async Task LoadAllAsync()
    {
        loading = true;
        await LoadHistoricalAsync();
        await LoadCashflowAsync(CurrentUser.UserId);
        loading = false;
    }

    private async Task LoadHistoricalAsync()
    {
        var userId = CurrentUser.UserId;
        months = InsightsDataService.GetTrendMonths(selectedMonth);
        try { spendError = string.Empty; spendPlan = await ReportsData.GetSpendVsPlanAsync(userId, selectedMonth); }
        catch { spendError = "Spend vs Plan is temporarily unavailable."; }
        try { trendError = string.Empty; trends = await ReportsData.GetCategoryTrendsAsync(userId, selectedMonth); }
        catch { trendError = "Category Trends is temporarily unavailable."; }
        try { netWorthError = string.Empty; netWorth = await ReportsData.GetNetWorthAsync(userId, selectedMonth); }
        catch { netWorthError = "Net Worth is temporarily unavailable."; }
    }

    private async Task LoadCashflowAsync(int userId)
    {
        try { cashflowError = string.Empty; cashflow = await ReportsData.GetCashflowAsync(userId, horizonDays); }
        catch { cashflowError = "Cashflow Forecast is temporarily unavailable."; }
    }
}
