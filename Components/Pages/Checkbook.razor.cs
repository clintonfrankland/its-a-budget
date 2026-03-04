using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class Checkbook
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
    private ClintonFranklandDbContext DbContext { get; set; } = default!;

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

            await LoadDataAsync();
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

    private async Task LoadDataAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var account = await DbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
            var startingBalance = account?.BeginningBalance ?? 0m;

            var transactionsData = await DbContext.Transactions
                .Include(t => t.Payee)
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Cleared)
                .ThenBy(t => t.TransactionDate)
                .ThenByDescending(t => t.Amount)
                .ToListAsync();

            // Calculate totals for balance display
            var totalAmount = transactionsData.Sum(t => t.Amount);
            var clearedAmount = transactionsData.Where(t => t.Cleared).Sum(t => t.Amount);
            balance = startingBalance + totalAmount;
            clearedBalance = startingBalance + clearedAmount;
            // Calculate running balance
            var runningBalance = startingBalance;
            transactions = transactionsData.Select(t =>
            {
                runningBalance += t.Amount;
                return new TransactionViewModel
                {
                    TransactionId = t.TransactionId,
                    TransactionDate = t.TransactionDate.ToDateTime(TimeOnly.MinValue),
                    PayeeName = t.Payee?.PayeeName ?? string.Empty,
                    CategoryName = t.Category?.CategoryName ?? string.Empty,
                    IsCleared = t.Cleared,
                    Amount = t.Amount,
                    Balance = runningBalance
                };
            }).ToList();

            // Load payees and categories for autocomplete (EF Core)
            await LoadPayeesAndCategoriesAsync();

            // Get forecast if needed
            await LoadForecastAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task LoadPayeesAndCategoriesAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var payees = await DbContext.Payees
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .OrderBy(p => p.PayeeName)
                .ToListAsync();

            payeesList = payees
                .Select(p => p.PayeeName)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();
            var categories = await DbContext.Categories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            categoriesList = categories
                .Select(c => c.CategoryName ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            // Don't fail if autocomplete data can't be loaded
            errorMessage = $"Warning: Could not load autocomplete data. {ex.Message}";
        }
    }

    private async Task LoadForecastAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            // Always check for bills due today (independent of budget panel settings)
            var allForecast = await GenerateBudgetForecastAsync(userId, DateTime.Today.AddDays(Math.Max(budgetDays + 1, 1)));

            var billsDueToday = allForecast
                .Where(b => b.DueDate <= DateTime.Today && b.IsBill)
                .ToList();

            showBillsDue = billsDueToday.Count > 0;
            billsDueCount = billsDueToday.Count;

            // Now load the budget panel data based on user's selected days
            if (budgetDays == 0)
            {
                budgetItems.Clear();
                return;
            }

            budgetItems = allForecast;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task<List<BudgetItemViewModel>> GenerateBudgetForecastAsync(int userId, DateTime endDate)
    {
        // Get starting balance from account
        var account = await DbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        var startingBalance = account?.BeginningBalance ?? 0m;

        // Add sum of all transactions to starting balance
        var transactionSum = await DbContext.Transactions
            .Where(t => t.UserId == userId)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        startingBalance += transactionSum;

        // Get all budgets for the user
        var budgets = await DbContext.Budgets
            .Include(b => b.Category)
            .Include(b => b.Frequency)
            .Include(b => b.Payee)
            .Where(b => b.UserId == userId)
            .ToListAsync();

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

    private async Task OnBudgetDaysChangedAsync(ChangeEventArgs e)
    {
        budgetDays = int.Parse(e.Value?.ToString() ?? "3");
        await LoadForecastAsync();
    }

    private async Task OnBudgetDaysChangedDropdownAsync()
    {
        await LoadForecastAsync();
    }

    private void ToggleBudgetItems()
    {
        showBudgetCollapse = !showBudgetCollapse;
    }

    // Handle budget action dropdown selection (for small screens)
    // When main button is clicked, args is null - default to "record" action
    private async Task OnBudgetActionSelectedOrDefaultAsync(RadzenSplitButtonItem? args, BudgetItemViewModel item)
    {
        var action = args?.Value?.ToString() ?? "record";  // Default to record when main button clicked

        switch (action)
        {
            case "record":
                ShowAddFromBudget(item);
                break;
            case "skip":
                await SkipBudgetAsync(item.BudgetId);
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
                await ShowEditTransactionAsync(txn.TransactionId);
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
    private async Task SkipBudgetAsync(int budgetId)
    {
        try
        {
            await MarkBudgetPaidAsync(budgetId);
            await LoadDataAsync();  // Refresh both transactions and budget items
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }
    private async Task MarkBudgetPaidAsync(int budgetId)
    {
        var budget = await DbContext.Budgets.FindAsync(budgetId);
        if (budget == null) return;

        // Calculate new NextDueDate based on frequency
        var newNextDueDate = CalculateNextDueDate(budget.NextDueDate ?? DateTime.Today, budget.FrequencyId ?? 0);
        budget.NextDueDate = newNextDueDate;
        await DbContext.SaveChangesAsync();

        // Delete if one-time (FrequencyId = 0)
        if (budget.FrequencyId == 0)
        {
            DbContext.Budgets.Remove(budget);
            await DbContext.SaveChangesAsync();
        }
        // Delete if past end date
        else if (budget.EndDate.HasValue && budget.EndDate != DateTime.Parse("1970-01-01") && newNextDueDate > budget.EndDate)
        {
            DbContext.Budgets.Remove(budget);
            await DbContext.SaveChangesAsync();
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

    private async Task ShowEditTransactionAsync(int transactionId)
    {
        try
        {
            SaveGridState();
            var transaction = await DbContext.Transactions
                .Include(t => t.Payee)
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (transaction != null)
            {
                editTransactionId = transactionId;
                editBudgetId = -1;  // Not from a budget item
                editDate = transaction.TransactionDate.ToDateTime(TimeOnly.MinValue);
                editPayee = transaction.Payee?.PayeeName ?? string.Empty;
                editCategory = transaction.Category?.CategoryName ?? string.Empty;
                var amount = transaction.Amount;
                editIsDebit = amount < 0;
                editAmount = Math.Abs(amount);
                editCleared = transaction.Cleared;
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

    private async Task SaveTransactionAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var finalAmount = editIsDebit ? -editAmount : editAmount;
            // Get or create Category and Payee (ensure they exist for required FK)
            var categoryId = await GetOrCreateCategoryAsync(editCategory, userId);
            var payeeId = await GetOrCreatePayeeAsync(editPayee, userId);

            // Default to -1 if not found (matching the stored procedure behavior)
            if (categoryId <= 0) categoryId = await GetOrCreateCategoryAsync("Uncategorized", userId);
            if (payeeId <= 0) payeeId = await GetOrCreatePayeeAsync("Unknown", userId);

            // Get default account
            var account = await DbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
            var accountId = account?.AccountId ?? 1;

            if (editTransactionId == -1)
            {
                // Insert new transaction
                var newTransaction = new Transaction
                {
                    UserId = userId,
                    TransactionDate = DateOnly.FromDateTime(editDate),
                    PayeeId = payeeId,
                    CategoryId = categoryId,
                    AccountId = accountId,
                    Amount = finalAmount,
                    Cleared = editCleared
                };
                DbContext.Transactions.Add(newTransaction);
            }
            else
            {
                // Update existing transaction
                var transaction = await DbContext.Transactions.FindAsync(editTransactionId);
                if (transaction != null)
                {
                    transaction.TransactionDate = DateOnly.FromDateTime(editDate);
                    transaction.PayeeId = payeeId;
                    transaction.CategoryId = categoryId;
                    transaction.Amount = finalAmount;
                    transaction.Cleared = editCleared;
                }
            }

            await DbContext.SaveChangesAsync();

            // If this transaction was from a budget item, mark it as paid (EF Core)
            if (editBudgetId != -1)
            {
                await MarkBudgetPaidAsync(editBudgetId);
                editBudgetId = -1;  // Reset
            }

            currentView = ViewMode.List;
            await LoadDataAsync();
            shouldRestoreGridState = true;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task<int> GetOrCreateCategoryAsync(string categoryName, int userId)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return -1;

        var category = await DbContext.Categories
            .FirstOrDefaultAsync(c => c.CategoryName == categoryName && c.UserId == userId);

        if (category != null)
            return category.CategoryId;

        // Create new category
        var newCategory = new Category { CategoryName = categoryName, UserId = userId };
        DbContext.Categories.Add(newCategory);
        await DbContext.SaveChangesAsync();
        return newCategory.CategoryId;
    }

    private async Task<int> GetOrCreatePayeeAsync(string payeeName, int userId)
    {
        if (string.IsNullOrWhiteSpace(payeeName))
            return -1;

        var payee = await DbContext.Payees
            .FirstOrDefaultAsync(p => p.PayeeName == payeeName && p.UserId == userId);

        if (payee != null)
            return payee.PayeeId;

        // Create new payee
        var newPayee = new Payee { PayeeName = payeeName, UserId = userId, IsDeleted = false };
        DbContext.Payees.Add(newPayee);
        await DbContext.SaveChangesAsync();
        return newPayee.PayeeId;
    }

    private async Task DeleteTransactionAsync()
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
            var transaction = await DbContext.Transactions.FindAsync(editTransactionId);
            if (transaction != null)
            {
                DbContext.Transactions.Remove(transaction);
                await DbContext.SaveChangesAsync();
            }

            currentView = ViewMode.List;
            await LoadDataAsync();
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
            var transaction = await DbContext.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Cleared = true;
                await DbContext.SaveChangesAsync();
            }

            // Update the item in place to preserve grid filter state
            var viewModel = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (viewModel != null)
            {
                viewModel.IsCleared = true;
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
            var transaction = await DbContext.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Cleared = false;
                await DbContext.SaveChangesAsync();
            }

            // Update the item in place to preserve grid filter state
            var viewModel = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (viewModel != null)
            {
                viewModel.IsCleared = false;
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
