using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class Budget
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private SiteInfoService SiteInfoService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private BudgetDataService BudgetData { get; set; } = default!;

    [Inject]
    private BudgetScheduleService BudgetSchedule { get; set; } = default!;

    private enum ViewMode { List, Edit, EditNext }
    private ViewMode currentView = ViewMode.List;

    private string errorMessage = string.Empty;
    private List<BudgetItemViewModel> budgetItems = new();
    private List<ChartDataPoint> chartData = new();
    private List<FrequencyOption> frequencyOptions = new();

    // Grid reference and search
    private RadzenDataGrid<BudgetItemViewModel>? budgetGrid;
    private string budgetSearchText = string.Empty;
    private IEnumerable<BudgetItemViewModel> filteredBudgetItems => FilterBudgetItems();

    // Autocomplete data for Category and Payee
    private List<string> categoriesList = new();
    private List<string> payeesList = new();

    // Screen size tracking for responsive column visibility
    private ScreenSize currentScreenSize = ScreenSize.Large;

    // Chart data point
    private record ChartDataPoint(DateTime Date, decimal Balance);

    // Filtered chart data based on screen size
    // ExtraExtraLarge/ExtraLarge/Large = 6 months, Medium = 3 months, Small = 2 months, ExtraSmall = 1 month
    private IEnumerable<ChartDataPoint> filteredChartData => FilterChartData();

    // Edit fields
    private int editBudgetId = -1;
    private string editBudgetName = string.Empty;
    private bool editIsExpense = true;  // true = Expense, false = Income
    private decimal editAmount = 0m;
    private DateTime editNextDueDate = DateTime.Today;
    private int editFrequencyId = 1;
    private string editCategory = string.Empty;
    private string editPayee = string.Empty;
    private bool editIsBill = false;
    private bool editIsAuto = false;
    private bool editIsLate = false;
    private bool editHasEndDate = false;
    private DateTime editEndDate = DateTime.Today;
    private string editErrorMessage = string.Empty;

    // Frequency dropdown option
    private record FrequencyOption(int FrequencyId, string FrequencyName);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (!AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/budget")}");
                return;
            }
            await LoadDataAsync();
            StateHasChanged();
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var endDate = DateTime.Today.AddMonths(6);
            var forecastItems = await BudgetSchedule.GetForecastAsync(userId, endDate);
            budgetItems = forecastItems;
            chartData = GenerateChartData(forecastItems);

            await LoadCategoriesAndPayeesAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private static List<ChartDataPoint> GenerateChartData(List<BudgetItemViewModel> forecastItems)
    {
        // Group by date and get the final balance for each date
        return forecastItems
            .GroupBy(i => i.DueDate.Date)
            .Select(g => new ChartDataPoint(g.Key, g.Last().Balance))
            .OrderBy(c => c.Date)
            .ToList();
    }

    private async Task LoadCategoriesAndPayeesAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var categories = await BudgetData.GetCategoriesForUserAsync(userId);

            categoriesList = categories
                .Select(c => c.CategoryName ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
            var payees = await BudgetData.GetPayeesForUserAsync(userId);

            payeesList = payees
                .Select(p => p.PayeeName)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"Warning: Could not load autocomplete data. {ex.Message}";
        }
    }

    private async Task ShowAddBudgetAsync()
    {
        await LoadFrequenciesAsync();
        editBudgetId = -1;
        editBudgetName = string.Empty;
        editIsExpense = true;
        editAmount = 0m;
        editNextDueDate = DateTime.Today;
        editFrequencyId = 1;
        editCategory = string.Empty;
        editPayee = string.Empty;
        editIsBill = false;
        editIsAuto = false;
        editIsLate = false;
        editHasEndDate = false;
        editEndDate = DateTime.Today;
        currentView = ViewMode.Edit;
    }

    private async Task ShowEditBudgetAsync(int budgetId)
    {
        try
        {
            await LoadFrequenciesAsync();
            var userId = SiteInfoService.DefaultUserId;
            var budget = await BudgetData.GetBudgetByIdAsync(userId, budgetId);

            if (budget != null)
            {
                editBudgetId = budgetId;
                editBudgetName = budget.BudgetName ?? string.Empty;
                editIsExpense = budget.BudgetTypeId == 1;  // 1 = Expense, 0 = Income
                editAmount = budget.Amount ?? 0m;
                editNextDueDate = budget.NextDueDate ?? DateTime.Today;
                editFrequencyId = budget.FrequencyId ?? 1;
                editCategory = budget.Category?.CategoryName ?? string.Empty;
                editPayee = budget.Payee?.PayeeName ?? string.Empty;
                editIsBill = budget.IsBill ?? false;
                editIsAuto = budget.IsAutomatic ?? false;
                editIsLate = budget.IsLate ?? false;

                var endDate = budget.EndDate ?? DateTime.Parse("1970-01-01");
                editHasEndDate = endDate != DateTime.Parse("1970-01-01");
                editEndDate = editHasEndDate ? endDate : DateTime.Today;

                currentView = ViewMode.Edit;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task LoadFrequenciesAsync()
    {
        try
        {
            var frequencies = await BudgetData.GetFrequenciesAsync();

            frequencyOptions = frequencies
                .Select(f => new FrequencyOption(f.FrequencyId, f.FrequencyName))
                .ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void CancelEdit()
    {
        editErrorMessage = string.Empty;
        currentView = ViewMode.List;
    }

    private async Task SaveBudgetAsync()
    {
        editErrorMessage = string.Empty;

        if (editHasEndDate && editEndDate < editNextDueDate)
        {
            editErrorMessage = "End date cannot be before the next due date.";
            return;
        }

        if (!CurrencyPolicy.TryValidateNonNegativeSqlAmount(editAmount, out var amountMessage))
        {
            editErrorMessage = amountMessage;
            return;
        }

        if (editNextDueDate == DateTime.MinValue)
        {
            editErrorMessage = "Next due date is required.";
            return;
        }

        if (editHasEndDate && editEndDate == DateTime.MinValue)
        {
            editErrorMessage = "End date is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(editBudgetName))
        {
            editErrorMessage = "Budget name is required.";
            return;
        }

        if (!frequencyOptions.Any(f => f.FrequencyId == editFrequencyId))
        {
            editErrorMessage = "Frequency is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(editCategory))
        {
            editErrorMessage = "Category is required.";
            return;
        }

        var payeeName = editIsBill ? editPayee?.Trim() ?? string.Empty : string.Empty;
        if (editIsBill && string.IsNullOrWhiteSpace(payeeName))
        {
            editErrorMessage = "Payee is required for bill items.";
            return;
        }

        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var endDate = editHasEndDate ? editEndDate : DateTime.Parse("1970-01-01");
            var roundedAmount = CurrencyPolicy.Round(editAmount);
            var budgetTypeId = editIsExpense ? 1 : 0;  // 1 = Expense, 0 = Income
            var isAuto = editIsBill && editIsAuto;
            var budgetName = editBudgetName.Trim();
            var categoryName = editCategory.Trim();
            await BudgetData.SaveBudgetAsync(
                userId,
                editBudgetId,
                budgetName,
                budgetTypeId,
                editFrequencyId,
                editNextDueDate,
                endDate,
                roundedAmount,
                categoryName,
                payeeName,
                isAuto,
                editIsBill,
                editIsLate);
            editErrorMessage = string.Empty;
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            editErrorMessage = $"Save failed: {ex.Message}";
        }
    }

    private async Task DeleteBudgetAsync()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to delete this budget item?",
            "Confirm Delete",
            new ConfirmOptions
            {
                OkButtonText = "Yes, Delete",
                CancelButtonText = "Cancel",
                CloseDialogOnOverlayClick = true
            });

        if (confirmed != true)
            return;

        try
        {
            var userId = SiteInfoService.DefaultUserId;
            await BudgetData.DeleteBudgetAsync(userId, editBudgetId);

            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task MarkPaidAsync(int budgetId)
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            await BudgetSchedule.MarkBudgetPaidAsync(userId, budgetId);

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task ShowEditNextAsync(int budgetId)
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var budget = await BudgetData.GetBudgetByIdAsync(userId, budgetId);

            if (budget != null)
            {
                editBudgetId = budgetId;
                editBudgetName = budget.BudgetName ?? string.Empty;
                editAmount = budget.Amount ?? 0m;
                editNextDueDate = budget.NextDueDate ?? DateTime.Today;
                editCategory = budget.Category?.CategoryName ?? string.Empty;
                editIsAuto = budget.IsAutomatic ?? false;
                editIsLate = budget.IsLate ?? false;

                currentView = ViewMode.EditNext;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task SaveEditNextAsync()
    {
        editErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(editBudgetName))
        {
            editErrorMessage = "Budget name is required.";
            return;
        }

        if (editNextDueDate == DateTime.MinValue)
        {
            editErrorMessage = "Due date is required.";
            return;
        }

        if (!CurrencyPolicy.TryValidateNonNegativeSqlAmount(editAmount, out var amountMessage))
        {
            editErrorMessage = amountMessage;
            return;
        }

        if (string.IsNullOrWhiteSpace(editCategory))
        {
            editErrorMessage = "Category is required.";
            return;
        }

        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var budgetName = editBudgetName.Trim();
            var categoryName = editCategory.Trim();

            await BudgetSchedule.CreateEditedNextOccurrenceAsync(
                userId,
                editBudgetId,
                budgetName,
                editNextDueDate,
                CurrencyPolicy.Round(editAmount),
                categoryName,
                editIsAuto,
                editIsLate);

            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            editErrorMessage = $"Save failed: {ex.Message}";
        }
    }

    // Screen size enum matching Bootstrap breakpoints
    public enum ScreenSize
    {
        ExtraSmall,  // < 576px
        Small,       // >= 576px
        Medium,      // >= 768px
        Large,       // >= 992px
        ExtraLarge,  // >= 1200px
        ExtraExtraLarge // >= 1400px
    }

    // Called by RadzenMediaQuery components when breakpoints change
    void OnScreenSizeChange(ScreenSize size, bool matches)
    {
        if (matches)
        {
            currentScreenSize = size;
            StateHasChanged();
        }
    }

    // Helper methods to check screen size for column visibility
    bool IsAtLeast(ScreenSize minimumSize) => currentScreenSize >= minimumSize;
    bool IsAtMost(ScreenSize maximumSize) => currentScreenSize <= maximumSize;

    // Handle budget grid action dropdown selection (for small screens)
    // When main button is clicked, args is null - default to "edit" action
    private async Task OnBudgetGridActionSelectedOrDefaultAsync(RadzenSplitButtonItem? args, BudgetItemViewModel item)
    {
        var action = args?.Value?.ToString() ?? "edit";  // Default to edit when main button clicked

        switch (action)
        {
            case "edit":
                await ShowEditBudgetAsync(item.BudgetId);
                break;
            case "paid":
                await MarkPaidAsync(item.BudgetId);
                break;
            case "editnext":
                await ShowEditNextAsync(item.BudgetId);
                break;
        }
    }

    // Filter chart data based on screen size
    // ExtraExtraLarge/ExtraLarge/Large = 6 months, Medium = 3 months, Small = 2 months, ExtraSmall = 1 month
    private IEnumerable<ChartDataPoint> FilterChartData()
    {
        if (chartData.Count == 0)
            return chartData;

        var months = currentScreenSize switch
        {
            ScreenSize.ExtraSmall => 1,
            ScreenSize.Small => 2,
            ScreenSize.Medium => 3,
            _ => 6  // Large, ExtraLarge, ExtraExtraLarge
        };

        var endDate = DateTime.Today.AddMonths(months);
        return chartData.Where(d => d.Date <= endDate);
    }

    // Get the X-axis step for the chart based on screen size / time range
    private TimeSpan GetChartXAxisStep()
    {
        return currentScreenSize switch
        {
            ScreenSize.ExtraSmall => TimeSpan.FromDays(7),   // 1 month = weekly ticks
            ScreenSize.Small => TimeSpan.FromDays(14),       // 2 months = bi-weekly ticks
            ScreenSize.Medium => TimeSpan.FromDays(14),      // 3 months = bi-weekly ticks
            _ => TimeSpan.FromDays(30)                        // 6 months = monthly ticks
        };
    }

    // Search functionality
    private IEnumerable<BudgetItemViewModel> FilterBudgetItems()
    {
        if (string.IsNullOrWhiteSpace(budgetSearchText))
            return budgetItems;

        var searchLower = budgetSearchText.ToLower();
        return budgetItems.Where(item =>
            (item.BudgetName?.ToLower().Contains(searchLower) ?? false) ||
            (item.Category?.ToLower().Contains(searchLower) ?? false) ||
            (item.Payee?.ToLower().Contains(searchLower) ?? false) ||
            (item.FrequencyName?.ToLower().Contains(searchLower) ?? false) ||
            item.Amount.ToString("C").ToLower().Contains(searchLower) ||
            item.DueDate.ToString("MM/dd/yyyy").Contains(searchLower)
        );
    }

    private void OnBudgetSearch(string? value)
    {
        budgetSearchText = value ?? string.Empty;
        budgetGrid?.GoToPage(0);
    }
}
