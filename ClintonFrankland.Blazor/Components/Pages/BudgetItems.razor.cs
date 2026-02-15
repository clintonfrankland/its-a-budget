using System.Data;
using ClintonFrankland.Models;
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
    private SqlProvider Sql { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    private enum ViewMode { List, Edit }
    private ViewMode currentView = ViewMode.List;

    private string errorMessage = string.Empty;
    private List<BudgetItemViewModel> budgetItems = new();
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
            LoadData();
            StateHasChanged();
        }
    }

    private void LoadData()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var data = Sql.GetDataTable(dbName, "dbo", "spcfGetBudgetItems_030000", new NamedValue("UserId", SiteInfoService.DefaultUserId));

            // Convert to view models for RadzenDataGrid
            budgetItems = data.AsEnumerable().Select(row => new BudgetItemViewModel
            {
                BudgetId = Convert.ToInt32(row["BudgetId"]),
                BudgetName = row["BudgetName"]?.ToString() ?? string.Empty,
                Category = row["Category"]?.ToString() ?? string.Empty,
                DueDate = ParseDateTime(row["DueDate"]),
                EndDateName = row["EndDateName"]?.ToString() ?? string.Empty,
                FrequencyName = row["FrequencyName"]?.ToString() ?? string.Empty,
                Amount = Convert.ToDecimal(row["Amount"]),
                Monthly = ParseDecimal(row["Monthly"]),
                IsBill = Convert.ToBoolean(row["IsBill"]),
                IsAuto = Convert.ToBoolean(row["IsAuto"]),
                IsLate = Convert.ToBoolean(row["IsLate"])
            }).ToList();

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

    // Helper to safely parse DateTime from database values (handles null, DBNull, and empty strings)
    private static DateTime ParseDateTime(object? value)
    {
        if (value == null || value == DBNull.Value)
            return DateTime.MinValue;

        var strValue = value.ToString();
        if (string.IsNullOrWhiteSpace(strValue))
            return DateTime.MinValue;

        return DateTime.TryParse(strValue, out var result) ? result : DateTime.MinValue;
    }

    // Helper to safely parse decimal from database values (handles null, DBNull, empty strings, and currency symbols)
    private static decimal ParseDecimal(object? value)
    {
        if (value == null || value == DBNull.Value)
            return 0m;

        var strValue = value.ToString();
        if (string.IsNullOrWhiteSpace(strValue))
            return 0m;

        // Remove currency symbols and commas
        strValue = strValue.Replace("$", "").Replace(",", "").Trim();

        return decimal.TryParse(strValue, out var result) ? result : 0m;
    }
}
