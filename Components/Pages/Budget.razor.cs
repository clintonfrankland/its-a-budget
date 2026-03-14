using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
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

    // Edit Next fields (stores original budget data for creating one-time budget)
    private int editNextOriginalBudgetTypeId = 1;
    private bool editNextOriginalIsBill = false;
    private string editNextOriginalPayee = string.Empty;

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
            var forecastItems = await GenerateBudgetForecastAsync(userId, endDate);
            budgetItems = forecastItems;
            chartData = GenerateChartData(forecastItems);

            // Load autocomplete data (EF Core)
            await LoadCategoriesAndPayeesAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task<List<BudgetItemViewModel>> GenerateBudgetForecastAsync(int userId, DateTime endDate)
    {
        // Get starting balance from account
        var account = await BudgetData.GetAccountForUserAsync(userId);
        var startingBalance = account?.BeginningBalance ?? 0m;

        // Add sum of all transactions to starting balance
        var transactionSum = await BudgetData.GetTransactionSumAsync(userId);
        startingBalance += transactionSum;

        // Get all budgets for the user
        var budgets = await BudgetData.GetBudgetsForUserAsync(userId);

        // Extend end date if there's income scheduled after it
        var incomeBudget = budgets
            .Where(b => b.BudgetTypeId == 0)
            .OrderBy(b => b.NextDueDate)
            .FirstOrDefault();
        if (incomeBudget?.NextDueDate > endDate)
            endDate = incomeBudget.NextDueDate.Value;

        // Project all budget items forward through time
        var projectedItems = new List<(int BudgetId, string BudgetName, string Category, DateTime DueDate, decimal Amount, int BudgetTypeId, int FrequencyId, string FrequencyName, bool IsAuto, bool IsBill, bool IsLate, string Payee)>();

        foreach (var budget in budgets)
        {
            var nextDue = budget.NextDueDate ?? DateTime.Today;
            var budgetEndDate = (budget.EndDate == null || budget.EndDate == DateTime.Parse("1970-01-01")) 
                ? endDate 
                : budget.EndDate.Value;
            var frequencyId = budget.FrequencyId ?? 0;

            // Project this budget forward until end date
            while (nextDue < endDate && nextDue < budgetEndDate)
            {
                var amount = budget.BudgetTypeId == 0 ? (budget.Amount ?? 0m) : -(budget.Amount ?? 0m);
                projectedItems.Add((
                    budget.BudgetId,
                    budget.BudgetName ?? string.Empty,
                    budget.Category?.CategoryName ?? string.Empty,
                    nextDue,
                    amount,
                    budget.BudgetTypeId,
                    frequencyId,
                    budget.Frequency?.FrequencyName ?? string.Empty,
                    budget.IsAutomatic ?? false,
                    budget.IsBill ?? false,
                    budget.IsLate ?? false,
                    budget.Payee?.PayeeName ?? string.Empty
                ));

                // Calculate next due date
                if (frequencyId == 0) break; // One-time
                nextDue = CalculateNextDueDate(nextDue, frequencyId);
            }
        }

        // Sort by date, then by amount (descending for income first)
        var sortedItems = projectedItems.OrderBy(i => i.DueDate).ThenByDescending(i => i.Amount).ToList();

        // Calculate running balance
        var runningBalance = startingBalance;
        var result = new List<BudgetItemViewModel>();
        foreach (var item in sortedItems)
        {
            runningBalance += item.Amount;
            result.Add(new BudgetItemViewModel
            {
                BudgetId = item.BudgetId,
                BudgetName = item.BudgetName,
                Category = item.Category,
                DueDate = item.DueDate,
                Amount = item.Amount,
                Balance = runningBalance,
                FrequencyName = item.FrequencyName,
                IsAuto = item.IsAuto,
                IsBill = item.IsBill,
                IsLate = item.IsLate,
                Payee = item.Payee
            });
        }

        return result;
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
            var budget = await BudgetData.GetBudgetByIdAsync(budgetId);

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

        // Validate end date must be after due date
        if (editHasEndDate && editEndDate < editNextDueDate)
        {
            editErrorMessage = "The end date must be after the next due date.";
            return;
        }

        if (editAmount < 0)
        {
            editErrorMessage = "Amount must be a non-negative value.";
            return;
        }

        if (editNextDueDate == DateTime.MinValue || (editHasEndDate && editEndDate == DateTime.MinValue))
        {
            editErrorMessage = "Please enter valid dates.";
            return;
        }

        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var endDate = editHasEndDate ? editEndDate : DateTime.Parse("1970-01-01");
            var roundedAmount = CurrencyPolicy.Round(editAmount);
            var budgetTypeId = editIsExpense ? 1 : 0;  // 1 = Expense, 0 = Income
            var payeeName = editIsBill ? editPayee : string.Empty;
            var isAuto = editIsBill && editIsAuto;
            // Get or create Category
            var categoryId = await GetOrCreateCategoryAsync(editCategory, userId);

            // Get or create Payee
            var payeeId = await GetOrCreatePayeeAsync(payeeName, userId);

            if (editBudgetId == -1)
            {
                // Insert new budget
                var newBudget = new Models.Entities.Budget
                {
                    BudgetName = editBudgetName,
                    BudgetTypeId = budgetTypeId,
                    FrequencyId = editFrequencyId,
                    NextDueDate = editNextDueDate,
                    EndDate = endDate,
                    Amount = roundedAmount,
                    CategoryId = categoryId,
                    UserId = userId,
                    IsAutomatic = isAuto,
                    IsBill = editIsBill,
                    IsLate = editIsLate,
                    PayeeId = payeeId > 0 ? payeeId : null
                };
                await BudgetData.SaveBudgetAsync(newBudget, isNew: true);
            }
            else
            {
                // Update existing budget
                var budget = await BudgetData.FindBudgetAsync(editBudgetId);
                if (budget != null)
                {
                    budget.BudgetName = editBudgetName;
                    budget.BudgetTypeId = budgetTypeId;
                    budget.FrequencyId = editFrequencyId;
                    budget.NextDueDate = editNextDueDate;
                    budget.EndDate = endDate;
                    budget.Amount = roundedAmount;
                    budget.CategoryId = categoryId;
                    budget.UserId = userId;
                    budget.IsAutomatic = isAuto;
                    budget.IsBill = editIsBill;
                    budget.IsLate = editIsLate;
                    budget.PayeeId = payeeId > 0 ? payeeId : null;
                    await BudgetData.SaveBudgetAsync(budget, isNew: false);
                }
            }
            editErrorMessage = string.Empty;
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private Task<int> GetOrCreateCategoryAsync(string categoryName, int userId)
        => BudgetData.GetOrCreateCategoryAsync(categoryName, userId);

    private Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
        => BudgetData.GetOrCreatePayeeAsync(payeeName, userId);

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
            var budget = await BudgetData.FindBudgetAsync(editBudgetId);
            if (budget != null)
            {
                await BudgetData.DeleteBudgetAsync(budget);
            }

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
            var budget = await BudgetData.FindBudgetAsync(budgetId);
            if (budget == null) return;

            // Calculate new NextDueDate based on frequency
            var newNextDueDate = CalculateNextDueDate(budget.NextDueDate ?? DateTime.Today, budget.FrequencyId ?? 0);
            budget.NextDueDate = newNextDueDate;
            await BudgetData.SaveBudgetAsync(budget, isNew: false);

            // Delete if one-time (FrequencyId = 0)
            if (budget.FrequencyId == 0)
            {
                await BudgetData.DeleteBudgetAsync(budget);
            }
            // Delete if past end date
            else if (budget.EndDate.HasValue && budget.EndDate != DateTime.Parse("1970-01-01") && newNextDueDate > budget.EndDate)
            {
                await BudgetData.DeleteBudgetAsync(budget);
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private static DateTime CalculateNextDueDate(DateTime currentDate, int frequencyId)
    {
        return frequencyId switch
        {
            0 => currentDate, // One-time - no change
            1 => currentDate.AddDays(7), // Weekly
            2 => currentDate.AddDays(14), // Bi-weekly
            4 => currentDate.AddMonths(1), // Monthly
            5 => currentDate.AddMonths(2), // Bi-monthly
            6 => currentDate.AddMonths(3), // Quarterly
            7 => currentDate.AddDays(35), // 5 weeks
            8 => CalculateSemiMonthly(currentDate), // Semi-monthly (1st and 15th)
            9 => currentDate.AddYears(1), // Yearly
            10 => currentDate.AddDays(5), // Every 5 days
            11 => currentDate.AddDays(42), // 6 weeks
            12 => currentDate.AddDays(21), // 3 weeks
            13 => currentDate.AddDays(28), // 4 weeks
            14 => currentDate.AddMonths(6), // Semi-annually
            _ => currentDate
        };
    }

    private static DateTime CalculateSemiMonthly(DateTime currentDate)
    {
        // If on 1st, go to 15th; otherwise go to 1st of next month
        if (currentDate.Day == 1)
            return currentDate.AddDays(14);
        else
            return new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);
    }

    private async Task ShowEditNextAsync(int budgetId)
    {
        try
        {
            var budget = await BudgetData.GetBudgetByIdAsync(budgetId);

            if (budget != null)
            {
                editBudgetId = budgetId;
                editBudgetName = budget.BudgetName ?? string.Empty;
                editAmount = budget.Amount ?? 0m;
                editNextDueDate = budget.NextDueDate ?? DateTime.Today;
                editCategory = budget.Category?.CategoryName ?? string.Empty;
                editIsAuto = budget.IsAutomatic ?? false;
                editIsLate = budget.IsLate ?? false;

                // Store original values needed for creating the one-time budget
                editNextOriginalBudgetTypeId = budget.BudgetTypeId;
                editNextOriginalIsBill = budget.IsBill ?? false;
                editNextOriginalPayee = budget.Payee?.PayeeName ?? string.Empty;

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
        try
        {
            var userId = SiteInfoService.DefaultUserId;

            // 1. Mark the original budget as paid (EF Core)
            var originalBudget = await BudgetData.FindBudgetAsync(editBudgetId);
            if (originalBudget != null)
            {
                var newNextDueDate = CalculateNextDueDate(originalBudget.NextDueDate ?? DateTime.Today, originalBudget.FrequencyId ?? 0);
                originalBudget.NextDueDate = newNextDueDate;
                await BudgetData.SaveBudgetAsync(originalBudget, isNew: false);

                // Delete if one-time or past end date
                if (originalBudget.FrequencyId == 0 ||
                    (originalBudget.EndDate.HasValue && originalBudget.EndDate != DateTime.Parse("1970-01-01") && newNextDueDate > originalBudget.EndDate))
                {
                    await BudgetData.DeleteBudgetAsync(originalBudget);
                }
            }

            // 2. Create a new one-time budget (FrequencyId = 0)
            var categoryId = await GetOrCreateCategoryAsync(editCategory, userId);
            var payeeId = await GetOrCreatePayeeAsync(editNextOriginalPayee, userId);

            var newBudget = new Models.Entities.Budget
            {
                BudgetName = editBudgetName,
                BudgetTypeId = editNextOriginalBudgetTypeId,
                FrequencyId = 0,  // 0 = One-time
                NextDueDate = editNextDueDate,
                EndDate = DateTime.Parse("1970-01-01"),
                Amount = editAmount,
                CategoryId = categoryId,
                UserId = userId,
                IsAutomatic = editIsAuto,
                IsBill = editNextOriginalIsBill,
                IsLate = editIsLate,
                PayeeId = payeeId > 0 ? payeeId : null
            };
            await BudgetData.SaveBudgetAsync(newBudget, isNew: true);

            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
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
