using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
    private CheckbookDataService CheckbookData { get; set; } = default!;

    [Inject]
    private BudgetScheduleService BudgetSchedule { get; set; } = default!;

    [Inject]
    private ReceiptAttachmentStorageService ReceiptAttachmentStorage { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "search")]
    private string? InitialSearch { get; set; }

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
    private string editNotes = string.Empty;
    private string? editAttachmentPath = null;
    private IBrowserFile? editAttachmentFile = null;
    private bool removeAttachment = false;

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

            transactionSearchText = InitialSearch ?? string.Empty;
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
            var account = await CheckbookData.GetAccountForUserAsync(userId);
            var startingBalance = account?.BeginningBalance ?? 0m;

            var transactionsData = await CheckbookData.GetTransactionsForUserAsync(userId);

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
                    Balance = runningBalance,
                    Notes = t.Notes,
                    HasAttachment = !string.IsNullOrEmpty(t.AttachmentPath)
                };
            }).ToList();

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
            var payees = await CheckbookData.GetPayeesForUserAsync(userId);

            payeesList = payees
                .Select(p => p.PayeeName)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();
            var categories = await CheckbookData.GetCategoriesForUserAsync(userId);

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
            var allForecast = await BudgetSchedule.GetForecastAsync(userId, DateTime.Today.AddDays(Math.Max(budgetDays + 1, 1)));

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
        var userId = SiteInfoService.DefaultUserId;
        await BudgetSchedule.MarkBudgetPaidAsync(userId, budgetId);
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
        editNotes = string.Empty;
        editAttachmentPath = null;
        editAttachmentFile = null;
        removeAttachment = false;
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
        editNotes = string.Empty;
        editAttachmentPath = null;
        editAttachmentFile = null;
        removeAttachment = false;
        currentView = ViewMode.Edit;
    }

    private async Task ShowEditTransactionAsync(int transactionId)
    {
        try
        {
            SaveGridState();
            var userId = SiteInfoService.DefaultUserId;
            var transaction = await CheckbookData.GetTransactionByIdAsync(userId, transactionId);

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
                editNotes = transaction.Notes ?? string.Empty;
                editAttachmentPath = transaction.AttachmentPath;
                editAttachmentFile = null;
                removeAttachment = false;
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
            if (editAmount < 0)
            {
                errorMessage = "Amount must be a non-negative value.";
                return;
            }

            if (editDate == DateTime.MinValue)
            {
                errorMessage = "Please enter a valid transaction date.";
                return;
            }

            var userId = SiteInfoService.DefaultUserId;
            var finalAmount = editIsDebit ? -editAmount : editAmount;
            finalAmount = CurrencyPolicy.Round(finalAmount);
            string? attachmentPath = editAttachmentPath;
            string? previousAttachmentPath = editAttachmentPath;
            string? newlySavedAttachmentPath = null;
            if (removeAttachment && !string.IsNullOrEmpty(editAttachmentPath))
            {
                attachmentPath = null;
            }
            if (editAttachmentFile != null)
            {
                var saveResult = await ReceiptAttachmentStorage.SaveAsync(editAttachmentFile, userId);
                if (!saveResult.Succeeded)
                {
                    errorMessage = saveResult.ErrorMessage ?? "The receipt could not be saved.";
                    return;
                }

                newlySavedAttachmentPath = saveResult.RelativePath;
                attachmentPath = newlySavedAttachmentPath;
            }

            try
            {
                await CheckbookData.SaveTransactionAsync(
                    userId,
                    editTransactionId,
                    DateOnly.FromDateTime(editDate),
                    editPayee,
                    editCategory,
                    finalAmount,
                    editCleared,
                    editNotes,
                    attachmentPath);
            }
            catch
            {
                await ReceiptAttachmentStorage.DeleteIfManagedAsync(newlySavedAttachmentPath);
                throw;
            }

            if ((removeAttachment || editAttachmentFile != null) && !string.IsNullOrEmpty(previousAttachmentPath))
            {
                await ReceiptAttachmentStorage.DeleteIfManagedAsync(previousAttachmentPath);
            }

            // If this transaction was from a budget item, mark it as paid.
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

    private void OnAttachmentSelected(InputFileChangeEventArgs e)
    {
        editAttachmentFile = e.File;
        removeAttachment = false;
    }

    private void RemoveAttachment()
    {
        editAttachmentFile = null;
        removeAttachment = true;
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
            var userId = SiteInfoService.DefaultUserId;
            var transaction = await CheckbookData.GetTransactionByIdAsync(userId, editTransactionId);
            if (transaction != null)
            {
                var attachmentPath = transaction.AttachmentPath;
                await CheckbookData.DeleteTransactionAsync(userId, editTransactionId);
                await ReceiptAttachmentStorage.DeleteIfManagedAsync(attachmentPath);
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
            var userId = SiteInfoService.DefaultUserId;
            await CheckbookData.SetTransactionClearedAsync(userId, transactionId, true);

            // Update the item in place to preserve grid filter state
            var viewModel = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (viewModel != null && !viewModel.IsCleared)
            {
                viewModel.IsCleared = true;
                clearedBalance += viewModel.Amount;
            }

            await checkbookGrid.Reload();
            StateHasChanged();
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
            var userId = SiteInfoService.DefaultUserId;
            await CheckbookData.SetTransactionClearedAsync(userId, transactionId, false);

            // Update the item in place to preserve grid filter state
            var viewModel = transactions.FirstOrDefault(t => t.TransactionId == transactionId);
            if (viewModel != null && viewModel.IsCleared)
            {
                viewModel.IsCleared = false;
                clearedBalance -= viewModel.Amount;
            }

            await checkbookGrid.Reload();
            StateHasChanged();
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
