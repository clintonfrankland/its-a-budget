using System.Data;
using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class Accounts
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
    private List<AccountViewModel> accounts = new();
    
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
            var userId = SiteInfoService.DefaultUserId;

            // EF Core replacement for spcfGetAccounts
            var accountsData = await DbContext.Accounts
                .Include(a => a.AccountType)
                .Where(a => a.UserId == userId && (a.IsDeleted == null || a.IsDeleted == false))
                .OrderBy(a => a.AccountName)
                .ToListAsync();

            accounts = accountsData.Select(a => new AccountViewModel
            {
                AccountId = a.AccountId,
                AccountName = a.AccountName,
                AccountType = a.AccountType?.AccountTypeName ?? string.Empty,
                LastUpdated = a.LastUpdated?.ToString("yyyy-MM-dd") ?? string.Empty,
                AccountNumber = a.AccountNumber ?? string.Empty,
                InterestRate = a.InterestRate ?? 0m,
                MinimumPayment = a.MinimumPayment ?? 0m,
                Balance = a.Balance,
                Ratio = a.Balance == 0 ? null : Math.Round((a.MinimumPayment ?? 0) / a.Balance * 100, 2)
            }).ToList();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void ShowAddAccount()
    {
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

    private async Task ShowEditAccountAsync(int accountId)
    {
        try
        {
            // EF Core replacement for spcfGetAccount
            var account = await DbContext.Accounts
                .Include(a => a.AccountType)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account != null)
            {
                editAccountId = accountId;
                editAccountName = account.AccountName;
                editAccountNumber = account.AccountNumber ?? string.Empty;
                editAccountType = account.AccountTypeId;
                editBalance = account.Balance;
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

    private async Task SaveAccountAsync()
    {
        try
        {
            var userId = SiteInfoService.DefaultUserId;
            var now = DateTime.UtcNow;

            // EF Core replacement for spcfSaveAccount
            if (editAccountId == -1)
            {
                // Insert new account
                var newAccount = new Account
                {
                    AccountName = editAccountName,
                    AccountNumber = editAccountNumber,
                    AccountTypeId = editAccountType,
                    Balance = editBalance,
                    CreditLimit = editCreditLimit,
                    AvailableCredit = editAvailableCredit,
                    DueDate = editDueDate,
                    MinimumPayment = editMinimumPayment,
                    InterestRate = editInterestRate,
                    WebUrl = editWebUrl,
                    BeginningBalance = 0m,
                    ClearedBalance = 0m,
                    IsDefault = false,
                    UserId = userId,
                    LastUpdated = now
                };
                DbContext.Accounts.Add(newAccount);
            }
            else
            {
                // Update existing account
                var account = await DbContext.Accounts.FindAsync(editAccountId);
                if (account != null)
                {
                    account.AccountName = editAccountName;
                    account.AccountNumber = editAccountNumber;
                    account.AccountTypeId = editAccountType;
                    account.Balance = editBalance;
                    account.CreditLimit = editCreditLimit;
                    account.AvailableCredit = editAvailableCredit;
                    account.DueDate = editDueDate;
                    account.MinimumPayment = editMinimumPayment;
                    account.InterestRate = editInterestRate;
                    account.WebUrl = editWebUrl;
                    account.LastUpdated = now;
                }
            }

            await DbContext.SaveChangesAsync();
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
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
            // EF Core replacement for spcfDeleteAccount
            var account = await DbContext.Accounts.FindAsync(editAccountId);
            if (account != null)
            {
                DbContext.Accounts.Remove(account);
                await DbContext.SaveChangesAsync();
            }

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
}

