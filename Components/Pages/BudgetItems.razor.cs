using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class BudgetItems
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
    private BudgetItemsDataService BudgetItemsData { get; set; } = default!;

    [Inject]
    private CheckbookDataService CheckbookData { get; set; } = default!;

    private enum ViewMode { List, Edit }
    private ViewMode currentView = ViewMode.List;

    private string errorMessage = string.Empty;
    private List<BudgetItemViewModel> budgetItems = new();
    private Dictionary<string, decimal[]> _sparklineData = new();
    private List<FrequencyOption> frequencyOptions = new();
    
    // Grid reference and search
    private RadzenDataGrid<BudgetItemViewModel>? budgetItemsGrid;
    private string searchText = string.Empty;
    private IEnumerable<BudgetItemViewModel> filteredBudgetItems => FilterBudgetItems();
    
    // Autocomplete data for Category and Payee
    private List<string> categoriesList = new();
    private List<string> payeesList = new();

    // Screen size tracking for responsive column visibility
    private ScreenSize currentScreenSize = ScreenSize.Large;

    // Frequency dropdown option
    private record FrequencyOption(int FrequencyId, string FrequencyName);

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (!AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/budgetitems")}");
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
            var budgetsData = await BudgetItemsData.GetBudgetsForUserAsync(userId);
            _sparklineData = await CheckbookData.GetMonthlyCategoryTotalsAsync(userId, 3);

            // Convert to view models for RadzenDataGrid
            budgetItems = budgetsData.Select(b => new BudgetItemViewModel
            {
                BudgetId = b.BudgetId,
                BudgetName = b.BudgetName ?? string.Empty,
                Category = b.Category?.CategoryName ?? string.Empty,
                DueDate = b.NextDueDate ?? DateTime.Today,
                EndDateName = (b.EndDate == null || b.EndDate == DateTime.Parse("1970-01-01"))
                    ? string.Empty
                    : b.EndDate.Value.ToString("MM/dd/yyyy"),
                FrequencyName = b.Frequency?.FrequencyName ?? string.Empty,
                Amount = b.Amount ?? 0m,
                Monthly = CalculateMonthlyAmount(b.Amount ?? 0m, b.FrequencyId ?? 0, b.BudgetTypeId),
                IsBill = b.IsBill ?? false,
                IsAuto = b.IsAutomatic ?? false,
                IsLate = b.IsLate ?? false,
                SparklineData = _sparklineData.TryGetValue(b.Category?.CategoryName ?? string.Empty, out var sd) ? sd : []
            }).ToList();

            // Load autocomplete data (EF Core)
            await LoadCategoriesAndPayeesAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private static decimal CalculateMonthlyAmount(decimal amount, int frequencyId, int budgetTypeId)
    {
        // BudgetTypeId: 0 = Income (positive), 1 = Expense (negative)
        var multiplier = budgetTypeId == 0 ? 1m : -1m;

        var monthly = frequencyId switch
        {
            1 => amount * 52m / 12m,      // Weekly
            2 => amount * 26m / 12m,      // Bi-weekly
            4 => amount,                   // Monthly
            5 => amount / 2m,              // Bi-monthly
            6 => amount / 3m,              // Quarterly
            7 => amount * 52m / 5m / 12m,  // 5 weeks
            8 => amount * 2m,              // Semi-monthly
            9 => amount / 12m,             // Yearly
            10 => amount * 73m / 12m,      // Every 5 days (365/5 = 73)
            11 => amount * 52m / 6m / 12m, // 6 weeks
            12 => amount * 52m / 3m / 12m, // 3 weeks
            13 => amount * 52m / 4m / 12m, // 4 weeks
            14 => amount / 6m,             // Semi-annually
            _ => 0m                        // One-time or unknown
        };

        return Math.Round(monthly * multiplier, 2);
    }

    private async Task LoadCategoriesAndPayeesAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var categories = await BudgetItemsData.GetCategoriesForUserAsync(userId);

            categoriesList = categories
                .Select(c => c.CategoryName ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
            var payees = await BudgetItemsData.GetPayeesForUserAsync(userId);

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

    private async Task LoadFrequenciesAsync()
    {
        try
        {
            var frequencies = await BudgetItemsData.GetFrequenciesAsync();

            frequencyOptions = frequencies
                .Select(f => new FrequencyOption(f.FrequencyId, f.FrequencyName))
                .ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
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
            var budget = await BudgetItemsData.GetBudgetByIdAsync(budgetId);

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
                await BudgetItemsData.SaveBudgetAsync(newBudget, isNew: true);
            }
            else
            {
                // Update existing budget
                var budget = await BudgetItemsData.FindBudgetAsync(editBudgetId);
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
                    await BudgetItemsData.SaveBudgetAsync(budget, isNew: false);
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
        => BudgetItemsData.GetOrCreateCategoryAsync(categoryName, userId);

    private Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
        => BudgetItemsData.GetOrCreatePayeeAsync(payeeName, userId);

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
            await BudgetItemsData.DeleteBudgetAsync(editBudgetId);
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    // Screen size enum
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

    // Search functionality
    private IEnumerable<BudgetItemViewModel> FilterBudgetItems()
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return budgetItems;

        var searchLower = searchText.ToLower();
        return budgetItems.Where(item =>
            (item.BudgetName?.ToLower().Contains(searchLower) ?? false) ||
            (item.Category?.ToLower().Contains(searchLower) ?? false) ||
            (item.Payee?.ToLower().Contains(searchLower) ?? false) ||
            (item.FrequencyName?.ToLower().Contains(searchLower) ?? false) ||
            item.Amount.ToString("C").ToLower().Contains(searchLower)
        );
    }

    private void OnSearch(string? value)
    {
        searchText = value ?? string.Empty;
        budgetItemsGrid?.GoToPage(0);
    }

    private MarkupString RenderSparkline(BudgetItemViewModel item)
    {
        const int svgW = 84;
        const int svgH = 32;
        const int barW = 22;
        const int maxBarH = 28;
        const int baseline = svgH - 2;
        const int barSpacing = 28; // barW (22) + gap (6)

        var isExpense = item.Monthly < 0;
        var rawData = item.SparklineData;
        // Normalise to positive spend magnitudes
        var data = rawData.Select(v => isExpense ? Math.Max(-v, 0m) : Math.Max(v, 0m)).ToArray();
        var budget = Math.Abs(item.Monthly);
        var maxVal = data.Length > 0 ? Math.Max(data.Max(), budget) : budget;
        if (maxVal == 0) return new MarkupString(string.Empty);

        var today = DateTime.Today;
        var monthLabels = Enumerable.Range(0, 3)
            .Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(-3 + i).ToString("MMM"))
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg width=\"{svgW}\" height=\"{svgH}\" xmlns=\"http://www.w3.org/2000/svg\" style=\"display:block;overflow:visible\">");

        for (var i = 0; i < 3; i++)
        {
            var barH = data.Length > i ? (int)(data[i] / maxVal * maxBarH) : 0;
            var barX = 3 + i * barSpacing;
            var label = i < monthLabels.Count ? monthLabels[i] : string.Empty;
            var amount = data.Length > i ? data[i].ToString("C0") : "$0";

            if (barH > 0)
            {
                var opacity = i == 2 ? "1" : "0.4";
                sb.Append($"<rect x=\"{barX}\" y=\"{baseline - barH}\" width=\"{barW}\" height=\"{barH}\" fill=\"#4A90D9\" rx=\"2\" opacity=\"{opacity}\"><title>{label}: {amount}</title></rect>");
            }
            else
            {
                sb.Append($"<rect x=\"{barX}\" y=\"{baseline - 2}\" width=\"{barW}\" height=\"2\" fill=\"#dee2e6\" rx=\"1\"><title>{label}: $0</title></rect>");
            }
        }

        if (budget > 0)
        {
            var lineY = (int)(baseline - budget / maxVal * maxBarH);
            if (lineY >= 0 && lineY <= svgH)
                sb.Append($"<line x1=\"0\" y1=\"{lineY}\" x2=\"{svgW}\" y2=\"{lineY}\" stroke=\"#FF6B6B\" stroke-width=\"1.5\" stroke-dasharray=\"4,3\"><title>Budget: {budget:C0}/mo</title></line>");
        }

        sb.Append("</svg>");
        return new MarkupString(sb.ToString());
    }
}
