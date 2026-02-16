using System.Data;
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
    private SqlProvider Sql { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

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
    private bool _initialized = false;

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
            LoadData();
            _initialized = true;
            StateHasChanged();
        }
    }

    private void LoadData()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // Load forecast data for the grid (6 months)
            var forecastData = Sql.GetDataTable(dbName, "dbo", "spcfGetMyBudget_020000",
                new NamedValue("EndDate", DateTime.Today.AddMonths(6)),
                new NamedValue("UserId", SiteInfoService.DefaultUserId));

            // Convert to view models for RadzenDataGrid
            budgetItems = forecastData.AsEnumerable().Select(row => new BudgetItemViewModel
            {
                BudgetId = Convert.ToInt32(row["BudgetId"]),
                DueDate = DateTime.Parse(row["DueDate"].ToString()!),
                BudgetName = row["BudgetName"]?.ToString() ?? string.Empty,
                Payee = row.Table.Columns.Contains("Payee") ? row["Payee"]?.ToString() ?? string.Empty : string.Empty,
                Category = row["Category"]?.ToString() ?? string.Empty,
                FrequencyName = row["FrequencyName"]?.ToString() ?? string.Empty,
                Amount = Convert.ToDecimal(row["Amount"]),
                Balance = Convert.ToDecimal(row["Balance"]),
                IsBill = Convert.ToBoolean(row["IsBill"]),
                IsAuto = Convert.ToBoolean(row["IsAuto"]),
                IsLate = Convert.ToBoolean(row["IsLate"])
            }).ToList();

            // Load chart data (60 days - separate stored procedure)
            var chartDataTable = Sql.GetDataTable(dbName, "dbo", "spcfMyBudgetGetChart",
                new NamedValue("EndDate", DateTime.Today.AddMonths(6)),
                new NamedValue("UserId", SiteInfoService.DefaultUserId));

            chartData = chartDataTable.AsEnumerable().Select(row => new ChartDataPoint(
                Convert.ToDateTime(row["Date"]),
                Convert.ToDecimal(row["Balance"])
            )).ToList();

            // Load autocomplete data
            LoadCategoriesAndPayees();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void LoadCategoriesAndPayees()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // Load categories
            var categoriesData = Sql.GetDataTable(dbName, "dbo", "spcfGetCategories_020000", new NamedValue("UserId", SiteInfoService.DefaultUserId));
            categoriesList = categoriesData.AsEnumerable()
                .Select(row => row["CategoryName"]?.ToString() ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Load payees
            var payeesData = Sql.GetDataTable(dbName, "dbo", "spcfGetPayees_020000", new NamedValue("UserId", SiteInfoService.DefaultUserId));
            payeesList = payeesData.AsEnumerable()
                .Select(row => row["PayeeName"]?.ToString() ?? string.Empty)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"Warning: Could not load autocomplete data. {ex.Message}";
        }
    }

    private void ShowAddBudget()
    {
        LoadFrequencies();
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

    private void ShowEditBudget(int budgetId)
    {
        try
        {
            LoadFrequencies();
            var dbName = SiteInfoService.DatabaseName;
            var row = Sql.GetDataRow(dbName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", budgetId));
            if (row != null)
            {
                editBudgetId = budgetId;
                editBudgetName = row["BudgetName"]?.ToString() ?? string.Empty;
                var budgetTypeId = Convert.ToInt32(row["BudgetTypeId"]);
                editIsExpense = budgetTypeId == 1;  // 1 = Expense, 0 = Income
                editAmount = Convert.ToDecimal(row["Amount"]);
                editNextDueDate = Convert.ToDateTime(row["NextDueDate"]);
                editFrequencyId = Convert.ToInt32(row["FrequencyId"]);
                editCategory = row["Category"]?.ToString() ?? string.Empty;
                editPayee = row["Payee"]?.ToString() ?? string.Empty;
                editIsBill = Convert.ToBoolean(row["IsBill"]);
                editIsAuto = Convert.ToBoolean(row["IsAuto"]);
                editIsLate = Convert.ToBoolean(row["IsLate"]);

                var endDate = Convert.ToDateTime(row["EndDate"]);
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

    private void LoadFrequencies()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var frequencies = Sql.GetDataTable(dbName, "dbo", "spcfGetFrequencies");
            frequencyOptions = frequencies.AsEnumerable()
                .Select(row => new FrequencyOption(
                    Convert.ToInt32(row["FrequencyId"]),
                    row["FrequencyName"]?.ToString() ?? string.Empty))
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

    private void SaveBudget()
    {
        editErrorMessage = string.Empty;

        // Validate end date must be after due date
        if (editHasEndDate && editEndDate < editNextDueDate)
        {
            editErrorMessage = "The end date must be after the next due date.";
            return;
        }

        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var endDate = editHasEndDate ? editEndDate : DateTime.Parse("1970-01-01");
            var budgetTypeId = editIsExpense ? 1 : 0;  // 1 = Expense, 0 = Income
            var payee = editIsBill ? editPayee : string.Empty;
            var isAuto = editIsBill && editIsAuto;

            Sql.ExecuteNonQuery(dbName, "dbo", "spcfSaveBudget_030000",
                new NamedValue("BudgetId", editBudgetId),
                new NamedValue("BudgetName", editBudgetName),
                new NamedValue("FrequencyId", editFrequencyId),
                new NamedValue("NextDueDate", editNextDueDate),
                new NamedValue("EndDate", endDate),
                new NamedValue("Amount", editAmount),
                new NamedValue("BudgetTypeId", budgetTypeId),
                new NamedValue("Category", editCategory),
                new NamedValue("UserId", SiteInfoService.DefaultUserId),
                new NamedValue("IsAuto", isAuto),
                new NamedValue("IsBill", editIsBill),
                new NamedValue("IsLate", editIsLate),
                new NamedValue("Payee", payee));

            editErrorMessage = string.Empty;
            currentView = ViewMode.List;
            LoadData();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task DeleteBudget()
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
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfDeleteBudget", new NamedValue("BudgetId", editBudgetId));
            currentView = ViewMode.List;
            LoadData();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void MarkPaid(int budgetId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", budgetId));
            LoadData();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void ShowEditNext(int budgetId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var row = Sql.GetDataRow(dbName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", budgetId));
            if (row != null)
            {
                editBudgetId = budgetId;
                editBudgetName = row["BudgetName"]?.ToString() ?? string.Empty;
                editAmount = Convert.ToDecimal(row["Amount"]);
                editNextDueDate = Convert.ToDateTime(row["NextDueDate"]);
                editCategory = row["Category"]?.ToString() ?? string.Empty;
                editIsAuto = Convert.ToBoolean(row["IsAuto"]);
                editIsLate = Convert.ToBoolean(row["IsLate"]);

                // Store original values needed for creating the one-time budget
                editNextOriginalBudgetTypeId = Convert.ToInt32(row["BudgetTypeId"]);
                editNextOriginalIsBill = Convert.ToBoolean(row["IsBill"]);
                editNextOriginalPayee = row["Payee"]?.ToString() ?? string.Empty;

                currentView = ViewMode.EditNext;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void SaveEditNext()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // 1. Mark the original budget as paid
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", editBudgetId));

            // 2. Create a new one-time budget (FrequencyId = 0)
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfSaveBudget_030000",
                new NamedValue("BudgetId", -1),  // -1 = new budget
                new NamedValue("BudgetName", editBudgetName),
                new NamedValue("FrequencyId", 0),  // 0 = One-time
                new NamedValue("NextDueDate", editNextDueDate),
                new NamedValue("EndDate", DateTime.Parse("1970-01-01")),
                new NamedValue("Amount", editAmount),
                new NamedValue("BudgetTypeId", editNextOriginalBudgetTypeId),
                new NamedValue("Category", editCategory),
                new NamedValue("UserId", SiteInfoService.DefaultUserId),
                new NamedValue("IsAuto", editIsAuto),
                new NamedValue("IsBill", editNextOriginalIsBill),
                new NamedValue("IsLate", editIsLate),
                new NamedValue("Payee", editNextOriginalPayee));

            currentView = ViewMode.List;
            LoadData();
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
    private void OnBudgetGridActionSelectedOrDefault(RadzenSplitButtonItem? args, BudgetItemViewModel item)
    {
        var action = args?.Value?.ToString() ?? "edit";  // Default to edit when main button clicked

        switch (action)
        {
            case "edit":
                ShowEditBudget(item.BudgetId);
                break;
            case "paid":
                MarkPaid(item.BudgetId);
                break;
            case "editnext":
                ShowEditNext(item.BudgetId);
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
