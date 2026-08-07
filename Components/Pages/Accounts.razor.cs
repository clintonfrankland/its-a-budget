using ClintonFrankland.Models;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class Accounts
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private CurrentUserContext CurrentUser { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private AccountsDataService AccountsData { get; set; } = default!;

    [Inject]
    private SharedBudgetDataService SharedBudgetData { get; set; } = default!;

    private enum ViewMode { List, Edit, AdjustOpeningBalance }
    private ViewMode currentView = ViewMode.List;

    private string errorMessage = string.Empty;
    private List<AccountViewModel> accounts = new();
    private bool canCreateFinancialData;
    private string activeBudgetName = string.Empty;
    
    // Grid reference and search
    private RadzenDataGrid<AccountViewModel>? accountsGrid;
    private string searchText = string.Empty;
    private IEnumerable<AccountViewModel> filteredAccounts => FilterAccounts();

    // Screen size tracking for responsive column visibility
    private ScreenSize currentScreenSize = ScreenSize.Large;

    // Account type options for dropdown
    private static readonly List<AccountTypeOption> accountTypeOptions = new()
    {
        new(1, "Checking"),
        new(2, "Credit Card"),
        new(3, "Loan"),
        new(4, "Taxes"),
        new(5, "Phone")
    };
    private record AccountTypeOption(int Id, string Name);

    // Edit fields
    private int editAccountId = -1;
    private string editAccountName = string.Empty;
    private string editAccountNumber = string.Empty;
    private int editAccountType = 1;
    private decimal editBalance = 0m;
    private decimal currentOpeningBalance = 0m;
    private decimal proposedOpeningBalance = 0m;
    private AccountsDataService.BalanceAdjustmentPreview? balanceAdjustmentPreview;
    private decimal editCreditLimit = 0m;
    private decimal editAvailableCredit = 0m;
    private int editDueDate = 1;
    private decimal editMinimumPayment = 0m;
    private decimal editInterestRate = 0m;
    private string editWebUrl = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AuthService.InitializeAsync();
            if (!AuthService.IsAuthenticated)
            {
                Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/accounts")}");
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
            var userId = CurrentUser.UserId;
            var accountsData = await AccountsData.GetAccountsForUserAsync(userId);
            var writableSharedBudgetIds = await SharedBudgetData.GetFinancialManagerSharedBudgetIdsAsync(userId);
            canCreateFinancialData = writableSharedBudgetIds.Count > 0;
            activeBudgetName = (await SharedBudgetData.GetActiveSharedBudgetSummaryAsync(userId))?.Name ?? string.Empty;

            accounts = accountsData.Select(a => new AccountViewModel
            {
                AccountId = a.AccountId,
                AccountName = a.AccountName,
                AccountType = a.AccountType?.AccountTypeName ?? string.Empty,
                LastUpdated = a.LastUpdated.ToString("yyyy-MM-dd"),
                AccountNumber = a.AccountNumber ?? string.Empty,
                InterestRate = a.InterestRate ?? 0m,
                MinimumPayment = a.MinimumPayment ?? 0m,
                Balance = a.Balance,
                Ratio = a.Balance == 0 ? null : Math.Round((a.MinimumPayment ?? 0) / a.Balance * 100, 2),
                CanManageFinancialData = CanManageFinancialData(userId, writableSharedBudgetIds, a.SharedBudgetId, a.UserId),
                IsDefault = a.IsDefault
            }).ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void ShowAddAccount()
    {
        if (!canCreateFinancialData)
            return;

        editAccountId = -1;
        editAccountName = string.Empty;
        editAccountNumber = string.Empty;
        editAccountType = 1;
        editBalance = 0m;
        editCreditLimit = 0m;
        editAvailableCredit = 0m;
        editDueDate = 1;
        editMinimumPayment = 0m;
        editInterestRate = 0m;
        editWebUrl = string.Empty;
        currentView = ViewMode.Edit;
    }

    private void ShowPlaidConnections() => Navigation.NavigateTo("/accounts/plaid");

    private async Task MakeDefaultAccountAsync(int accountId)
    {
        if (await AccountsData.SetDefaultAccountAsync(CurrentUser.UserId, accountId, DateTime.UtcNow))
            await LoadDataAsync();
    }

    private async Task ShowEditAccountAsync(int accountId)
    {
        try
        {
            var userId = CurrentUser.UserId;
            var account = await AccountsData.GetAccountByIdAsync(userId, accountId);
            if (account is not null &&
                !await SharedBudgetData.CanManageFinancialDataAsync(userId, account.SharedBudgetId, account.UserId))
            {
                return;
            }

            if (account != null)
            {
                editAccountId = accountId;
                editAccountName = account.AccountName;
                editAccountNumber = account.AccountNumber ?? string.Empty;
                editAccountType = account.AccountTypeId;
                editBalance = account.Balance;
                currentOpeningBalance = account.BeginningBalance;
                editCreditLimit = account.CreditLimit ?? 0m;
                editAvailableCredit = account.AvailableCredit ?? 0m;
                editDueDate = account.DueDate ?? 1;
                editMinimumPayment = account.MinimumPayment ?? 0m;
                editInterestRate = account.InterestRate ?? 0m;
                editWebUrl = account.WebUrl ?? string.Empty;
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
        currentView = ViewMode.List;
    }

    private async Task ShowAdjustOpeningBalanceAsync()
    {
        proposedOpeningBalance = currentOpeningBalance;
        balanceAdjustmentPreview = await AccountsData.GetBalanceAdjustmentPreviewAsync(
            CurrentUser.UserId, editAccountId, proposedOpeningBalance);
        currentView = ViewMode.AdjustOpeningBalance;
    }

    private async Task PreviewOpeningBalanceAsync()
    {
        if (!CurrencyPolicy.FitsSqlDecimal(proposedOpeningBalance))
        {
            errorMessage = CurrencyPolicy.AmountTooLargeMessage;
            balanceAdjustmentPreview = null;
            return;
        }

        errorMessage = string.Empty;
        balanceAdjustmentPreview = await AccountsData.GetBalanceAdjustmentPreviewAsync(
            CurrentUser.UserId, editAccountId, proposedOpeningBalance);
    }

    private async Task SaveOpeningBalanceAdjustmentAsync()
    {
        await PreviewOpeningBalanceAsync();
        if (balanceAdjustmentPreview is null)
            return;

        var confirmed = await DialogService.Confirm(
            $"This changes the account's current balance from {balanceAdjustmentPreview.CurrentBalance:C2} " +
            $"to {balanceAdjustmentPreview.ProposedBalance:C2} and its cleared balance from " +
            $"{balanceAdjustmentPreview.CurrentClearedBalance:C2} to {balanceAdjustmentPreview.ProposedClearedBalance:C2}. Continue?",
            "Confirm Opening Balance Adjustment",
            new ConfirmOptions { OkButtonText = "Adjust Balance", CancelButtonText = "Cancel" });
        if (confirmed != true)
            return;

        await AccountsData.AdjustOpeningBalanceAsync(
            CurrentUser.UserId, editAccountId, proposedOpeningBalance, DateTime.UtcNow);
        currentView = ViewMode.List;
        await LoadDataAsync();
    }

    private void CancelOpeningBalanceAdjustment() => currentView = ViewMode.Edit;

    private async Task SaveAccountAsync()
    {
        try
        {
            if (!TryValidateAccountAmounts(out var amountMessage))
            {
                errorMessage = amountMessage;
                return;
            }

            var userId = CurrentUser.UserId;
            var now = DateTime.UtcNow;
            await AccountsData.SaveAccountAsync(
                userId,
                editAccountId,
                editAccountName,
                editAccountNumber,
                editAccountType,
                editBalance,
                editCreditLimit,
                editAvailableCredit,
                editDueDate,
                editMinimumPayment,
                editInterestRate,
                editWebUrl,
                now);
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private bool TryValidateAccountAmounts(out string message)
    {
        var values = new[] { editCreditLimit, editAvailableCredit, editMinimumPayment, editInterestRate };
        if (editAccountId == -1 &&
            !CurrencyPolicy.TryValidateNonNegativeSqlAmount(editBalance, out message))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (!CurrencyPolicy.TryValidateNonNegativeSqlAmount(value, out message))
                return false;
        }

        message = string.Empty;
        return true;
    }

    private async Task DeleteAccountAsync()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to delete this account?",
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
            var userId = CurrentUser.UserId;
            await AccountsData.DeleteAccountAsync(userId, editAccountId);
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

    // Search functionality
    private IEnumerable<AccountViewModel> FilterAccounts()
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return accounts;

        var searchLower = searchText.ToLower();
        return accounts.Where(a =>
            (a.AccountName?.ToLower().Contains(searchLower) ?? false) ||
            (a.AccountType?.ToLower().Contains(searchLower) ?? false) ||
            (a.AccountNumber?.ToLower().Contains(searchLower) ?? false) ||
            a.Balance.ToString("C").ToLower().Contains(searchLower)
        );
    }

    private void OnSearch(string? value)
    {
        searchText = value ?? string.Empty;
    }

    private static bool CanManageFinancialData(
        int userId,
        IReadOnlyCollection<int> writableSharedBudgetIds,
        int? sharedBudgetId,
        int? ownerUserId) =>
        sharedBudgetId.HasValue
            ? writableSharedBudgetIds.Contains(sharedBudgetId.Value)
            : ownerUserId == userId;
}
