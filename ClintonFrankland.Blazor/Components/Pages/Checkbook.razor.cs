using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using System;
using System.Data;

namespace ClintonFrankland.Components.Pages;

public partial class Checkbook
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
    private decimal balance = 0m;
    private decimal clearedBalance = 0m;
    private bool showBillsDue = false;
    private int billsDueCount = 0;
    private bool showBudgetCollapse = false;
    private int budgetDays = 3;

    // Budget days dropdown options
    private static readonly List<BudgetDaysOption> budgetDaysOptions = new()
    {
        new(0, "None"),
        new(1, "1 day"),
        new(3, "3 days"),
        new(7, "7 days"),
        new(14, "14 days"),
        new(21, "21 days"),
        new(30, "30 days")
    };
    private record BudgetDaysOption(int Value, string Text);

    // Transaction type dropdown options (Expense/Income)
    private static readonly List<TransactionTypeOption> transactionTypeOptions = new()
    {
        new(true, "Expense"),
        new(false, "Income")
    };
    private record TransactionTypeOption(bool Value, string Text);

    private DataTable? transactionData;
    private List<TransactionViewModel> transactions = new();
    private List<BudgetItemViewModel> budgetItems = new();
    RadzenDataGrid<TransactionViewModel> checkbookGrid = null!;

    // Search functionality
    private string transactionSearchText = string.Empty;
    private IEnumerable<TransactionViewModel> filteredTransactions => FilterTransactions();

    // Flag to apply initial filter only once after data loads
    private bool shouldApplyInitialFilter = true;

    // Flag to restore grid state after returning from edit
    private bool shouldRestoreGridState = false;

    // Screen size tracking for responsive column visibility
    private ScreenSize currentScreenSize = ScreenSize.Large;

    // Grid state preservation
    private Dictionary<string, object?> savedFilterValues = new();
    private Dictionary<string, SortOrder?> savedSortOrders = new();
    private int savedCurrentPage = 0;

    // Autocomplete data for Payees and Categories
    private List<string> payeesList = new();
    private List<string> categoriesList = new();

    // Edit fields
    private int editTransactionId = -1;
    private int editBudgetId = -1;  // Track if editing from a budget item
    private DateTime editDate = DateTime.Today;
    private string editPayee = string.Empty;
    private string editCategory = string.Empty;
    private decimal editAmount = 0m;
    private bool editIsDebit = true;
    private bool editCleared = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (!AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/checkbook")}");
                return;
            }

            LoadData();
            StateHasChanged();
        }

        // Apply initial filter after grid is rendered with data
        if (shouldApplyInitialFilter && checkbookGrid != null && transactions.Count > 0)
        {
            shouldApplyInitialFilter = false;
            var column = checkbookGrid.ColumnsCollection.FirstOrDefault(c => c.Property == "IsCleared");
            if (column != null)
            {
                // For CheckBoxList filter mode, set to show only uncleared (false) transactions
                await column.SetFilterValueAsync(new List<bool> { false });
            }
        }

        // Restore grid state after returning from edit view
        if (shouldRestoreGridState && checkbookGrid != null && transactions.Count > 0)
        {
            shouldRestoreGridState = false;
            await RestoreGridState();
        }
    }

    void OnFilter(DataGridColumnFilterEventArgs<TransactionViewModel> args)
    {
        Console.WriteLine($"Filter applied on column {args.Column.Property} with value {args.FilterValue}");
    }

    private void LoadData()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // Get balance
            var balanceData = Sql.GetDataTable(dbName, "dbo", "spcfGetCheckbookBalance_020000", new NamedValue("UserId", SiteInfoService.DefaultUserId));
            if (balanceData.Rows.Count > 0)
            {
                balance = Convert.ToDecimal(balanceData.Rows[0]["Balance"]);
                clearedBalance = Convert.ToDecimal(balanceData.Rows[0]["Cleared"]);
            }

            // Get transactions
            transactionData = Sql.GetDataTable(dbName, "dbo", "spcfGetMyCheckbook_040100", new NamedValue("UserId", SiteInfoService.DefaultUserId));

            // Convert to view models for RadzenDataGrid
            transactions = transactionData.AsEnumerable().Select(row => new TransactionViewModel
            {
                TransactionId = Convert.ToInt32(row["TransactionId"]),
                TransactionDate = DateTime.Parse(row["TransactionDate"].ToString()!),
                PayeeName = row["PayeeName"]?.ToString() ?? string.Empty,
                CategoryName = row["CategoryName"]?.ToString() ?? string.Empty,
                IsCleared = Convert.ToBoolean(row["ShowCleared"]),
                Amount = Convert.ToDecimal(row["Amount"]),
                Balance = Convert.ToDecimal(row["Balance"])
            }).ToList();

            // Load payees and categories for autocomplete
            LoadPayeesAndCategories();

            // Get forecast if needed
            LoadForecast();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void LoadPayeesAndCategories()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // Load payees
            var payeesData = Sql.GetDataTable(dbName, "dbo", "spcfGetPayees_020000", new NamedValue("UserId", SiteInfoService.DefaultUserId));
            payeesList = payeesData.AsEnumerable()
                .Select(row => row["PayeeName"]?.ToString() ?? string.Empty)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Load categories
            var categoriesData = Sql.GetDataTable(dbName, "dbo", "spcfGetCategories_020000", new NamedValue("UserId", SiteInfoService.DefaultUserId));
            categoriesList = categoriesData.AsEnumerable()
                .Select(row => row["CategoryName"]?.ToString() ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
        catch (Exception ex)
        {
            // Don't fail if autocomplete data can't be loaded
            errorMessage = $"Warning: Could not load autocomplete data. {ex.Message}";
        }
    }

    private void LoadForecast()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;

            // Always check for bills due today (independent of budget panel settings)
            var billsDueData = Sql.GetDataTable(dbName, "dbo", "spcfGetMyBudget_020000",
                new NamedValue("EndDate", DateTime.Today.AddDays(1)),
                new NamedValue("UserId", SiteInfoService.DefaultUserId));

            var billsDueToday = billsDueData.AsEnumerable()
                .Where(row => DateTime.Parse(row["DueDate"].ToString()!) <= DateTime.Today 
                              && Convert.ToBoolean(row["IsBill"]))
                .ToList();

            showBillsDue = billsDueToday.Count > 0;
            billsDueCount = billsDueToday.Count;

            // Now load the budget panel data based on user's selected days
            if (budgetDays == 0)
            {
                budgetItems.Clear();
                return;
            }

            var forecastData = Sql.GetDataTable(dbName, "dbo", "spcfGetMyBudget_020000",
                new NamedValue("EndDate", DateTime.Today.AddDays(budgetDays + 1)),
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
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void OnBudgetDaysChanged(ChangeEventArgs e)
    {
        budgetDays = int.Parse(e.Value?.ToString() ?? "3");
        LoadForecast();
    }

    private void OnBudgetDaysChangedDropdown()
    {
        LoadForecast();
    }

    private void ToggleBudgetItems()
    {
        showBudgetCollapse = !showBudgetCollapse;
    }

    // Handle budget action dropdown selection (for small screens)
    // When main button is clicked, args is null - default to "record" action
    private void OnBudgetActionSelectedOrDefault(RadzenSplitButtonItem? args, BudgetItemViewModel item)
    {
        var action = args?.Value?.ToString() ?? "record";  // Default to record when main button clicked

        switch (action)
        {
            case "record":
                ShowAddFromBudget(item);
                break;
            case "skip":
                SkipBudget(item.BudgetId);
                break;
        }
    }

    // Handle transaction action dropdown selection (for small screens)
    // When main button is clicked, args is null - default to "edit" action
    private async Task OnTransactionActionSelectedOrDefault(RadzenSplitButtonItem? args, TransactionViewModel txn)
    {
        var action = args?.Value?.ToString() ?? "edit";  // Default to edit when main button clicked

        switch (action)
        {
            case "edit":
                ShowEditTransaction(txn.TransactionId);
                break;
            case "cleared":
                await OnClearedChanged(txn.TransactionId, true);
                break;
            case "uncleared":
                await OnClearedChanged(txn.TransactionId, false);
                break;
        }
    }

    // Skip a budget item (mark as paid without creating a transaction)
    private void SkipBudget(int budgetId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", budgetId));
            LoadData();  // Refresh both transactions and budget items
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    // Save the current grid state (filters, sorts, page)
    private void SaveGridState()
    {
        if (checkbookGrid == null) return;


        savedFilterValues.Clear();
        savedSortOrders.Clear();

        foreach (var column in checkbookGrid.ColumnsCollection)
        {
            if (!string.IsNullOrEmpty(column.Property))
            {
                savedFilterValues[column.Property] = column.GetFilterValue();
                savedSortOrders[column.Property] = column.SortOrder;
            }
        }

        savedCurrentPage = checkbookGrid.CurrentPage;
    }

    // Restore the saved grid state
    private async Task RestoreGridState()
    {
        if (checkbookGrid == null) return;

        foreach (var column in checkbookGrid.ColumnsCollection)
        {
            if (!string.IsNullOrEmpty(column.Property))
            {
                if (savedFilterValues.TryGetValue(column.Property, out var filterValue) && filterValue != null)
                {
                    await column.SetFilterValueAsync(filterValue);
                }

                // Restore sort order - SortOrder is a public property we can set
                if (savedSortOrders.TryGetValue(column.Property, out var sortOrder))
                {
                    column.SortOrder = sortOrder;
                }
            }
        }

        // Reload the grid to apply the restored sort orders
        await checkbookGrid.Reload();

        if (savedCurrentPage > 0)
        {
            await checkbookGrid.GoToPage(savedCurrentPage);
        }
    }

    private void ShowAddTransaction()
    {
        SaveGridState();
        editTransactionId = -1;
        editBudgetId = -1;  // Not from a budget item
        editDate = DateTime.Today;
        editPayee = string.Empty;
        editCategory = string.Empty;
        editAmount = 0m;
        editIsDebit = true;
        editCleared = false;
        currentView = ViewMode.Edit;
    }

    // Load a budget item into the edit form for recording as a transaction
    private void ShowAddFromBudget(BudgetItemViewModel budgetItem)
    {
        SaveGridState();
        editTransactionId = -1;  // New transaction
        editBudgetId = budgetItem.BudgetId;  // Track the budget item
        editDate = budgetItem.DueDate;
        editPayee = !string.IsNullOrEmpty(budgetItem.Payee) ? budgetItem.Payee : budgetItem.BudgetName;
        editCategory = budgetItem.Category;
        editAmount = Math.Abs(budgetItem.Amount);
        editIsDebit = budgetItem.Amount < 0;
        editCleared = false;
        currentView = ViewMode.Edit;
    }

    private void ShowEditTransaction(int transactionId)
    {
        try
        {
            SaveGridState();
            var dbName = SiteInfoService.DatabaseName;
            var row = Sql.GetDataRow(dbName, "dbo", "spcfGetTransaction", new NamedValue("TransactionId", transactionId));
            if (row != null)
            {
                editTransactionId = transactionId;
                editBudgetId = -1;  // Not from a budget item
                editDate = Convert.ToDateTime(row["TransactionDate"]);
                editPayee = row["Payee"]?.ToString() ?? string.Empty;
                editCategory = row["Category"]?.ToString() ?? string.Empty;
                var amount = Convert.ToDecimal(row["Amount"]);
                editIsDebit = amount < 0;
                editAmount = Math.Abs(amount);
                editCleared = Convert.ToBoolean(row["Cleared"]);
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
        editBudgetId = -1;  // Clear the budget tracking
        currentView = ViewMode.List;
        shouldRestoreGridState = true;
    }

    private void SaveTransaction()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var finalAmount = editIsDebit ? -editAmount : editAmount;

            Sql.ExecuteNonQuery(dbName, "dbo", "spcfSaveTransaction_030000",
                new NamedValue("TransactionId", editTransactionId),
                new NamedValue("UserId", SiteInfoService.DefaultUserId),
                new NamedValue("TransactionDate", editDate),
                new NamedValue("Payee", editPayee),
                new NamedValue("Category", editCategory),
                new NamedValue("Amount", finalAmount),
                new NamedValue("Cleared", editCleared));

            // If this transaction was from a budget item, mark it as paid
            if (editBudgetId != -1)
            {
                Sql.ExecuteNonQuery(dbName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", editBudgetId));
                editBudgetId = -1;  // Reset
            }

            currentView = ViewMode.List;
            LoadData();
            shouldRestoreGridState = true;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task DeleteTransaction()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to delete this transaction?", 
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
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMyCheckbookDeleteTransaction", new NamedValue("TransactionId", editTransactionId));
            currentView = ViewMode.List;
            LoadData();
            shouldRestoreGridState = true;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task MarkCleared(int transactionId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMyCheckboxMarkTransactionCleared", new NamedValue("TransactionId", transactionId));

            // Update the item in place to preserve grid filter state
            var transaction = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (transaction != null)
            {
                transaction.IsCleared = true;
            }
            await checkbookGrid.Reload();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task MarkUncleared(int transactionId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfMyCheckboxMarkTransactionUncleared", new NamedValue("TransactionId", transactionId));

            // Update the item in place to preserve grid filter state
            var transaction = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (transaction != null)
            {
                transaction.IsCleared = false;
            }
            await checkbookGrid.Reload();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task OnClearedChanged(int transactionId, bool isCleared)
    {
        if (isCleared)
        {
            await MarkCleared(transactionId);
        }
        else
        {
            await MarkUncleared(transactionId);
        }
    }

    private void OnIsDebitChanged(ChangeEventArgs e)
    {
        editIsDebit = bool.Parse(e.Value?.ToString() ?? "true");
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
    bool IsBetween(ScreenSize minSize, ScreenSize maxSize) => currentScreenSize >= minSize && currentScreenSize <= maxSize;
    bool IsExactly(ScreenSize size) => currentScreenSize == size;

    // Search functionality
    private IEnumerable<TransactionViewModel> FilterTransactions()
    {
        if (string.IsNullOrWhiteSpace(transactionSearchText))
            return transactions;

        var searchLower = transactionSearchText.ToLower();
        return transactions.Where(txn =>
            (txn.PayeeName?.ToLower().Contains(searchLower) ?? false) ||
            (txn.CategoryName?.ToLower().Contains(searchLower) ?? false) ||
            txn.Amount.ToString("C").ToLower().Contains(searchLower) ||
            txn.TransactionDate.ToString("MM/dd/yyyy").Contains(searchLower)
        );
    }

    private void OnTransactionSearch(string? value)
    {
        transactionSearchText = value ?? string.Empty;
        checkbookGrid?.GoToPage(0);
    }
}
