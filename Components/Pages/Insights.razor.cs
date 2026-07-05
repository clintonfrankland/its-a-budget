using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ClintonFrankland.Components.Pages;

public partial class Insights
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private CurrentUserContext CurrentUser { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private InsightsDataService InsightsData { get; set; } = default!;

    private string errorMessage = string.Empty;
    private DateOnly selectedMonth = DateOnly.FromDateTime(DateTime.Today);
    private List<CategorySpendingTotal> categoryTotals = new();
    private List<CategoryTrendRow> categoryTrends = new();
    private List<TopPayeeSpending> topPayees = new();
    private List<InsightsMonth> trendMonths = new();

    private string SelectedMonthInput => selectedMonth.ToString("yyyy-MM");
    private string SelectedMonthLabel => selectedMonth.ToString("MMMM yyyy");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/insights")}");
            return;
        }

        selectedMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task OnSelectedMonthChangedAsync(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString();
        if (string.IsNullOrWhiteSpace(rawValue) ||
            !DateOnly.TryParseExact($"{rawValue}-01", "yyyy-MM-dd", out var parsedMonth))
        {
            return;
        }

        selectedMonth = parsedMonth;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            errorMessage = string.Empty;
            var userId = CurrentUser.UserId;
            trendMonths = InsightsDataService.GetTrendMonths(selectedMonth);
            categoryTotals = await InsightsData.GetMonthlyCategoryTotalsAsync(userId, selectedMonth);
            categoryTrends = await InsightsData.GetCategoryTrendAsync(userId, selectedMonth);
            topPayees = await InsightsData.GetTopPayeesAsync(userId, selectedMonth);
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

}
