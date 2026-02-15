using System.Data;
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
            LoadData();
            StateHasChanged();
        }
    }

    private void LoadData()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var data = Sql.GetDataTable(dbName, "dbo", "spcfGetAccounts",
                new NamedValue("userid", SiteInfoService.DefaultUserId),
                new NamedValue("accounttype", -1));

            accounts = data.AsEnumerable().Select(row => new AccountViewModel
            {
                AccountId = Convert.ToInt32(row["AccountId"]),
                AccountName = row["AccountName"]?.ToString() ?? string.Empty,
                AccountType = row["AccountType"]?.ToString() ?? string.Empty,
                LastUpdated = row["LastUpdated"]?.ToString() ?? string.Empty,
                AccountNumber = row["AccountNumber"]?.ToString() ?? string.Empty,
                InterestRate = row["InterestRate"] != DBNull.Value ? Convert.ToDecimal(row["InterestRate"]) : 0m,
                MinimumPayment = row["MinimumPayment"] != DBNull.Value ? Convert.ToDecimal(row["MinimumPayment"]) : 0m,
                Balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0m,
                Ratio = row["Ratio"] != DBNull.Value ? Convert.ToDecimal(row["Ratio"]) : null
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

    private void ShowEditAccount(int accountId)
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            var row = Sql.GetDataRow(dbName, "dbo", "spcfGetAccount", new NamedValue("accountid", accountId));
            if (row != null)
            {
                editAccountId = accountId;
                editAccountName = row["AccountName"]?.ToString() ?? string.Empty;
                editAccountNumber = row["AccountNumber"]?.ToString() ?? string.Empty;
                editAccountType = row["AccountType"] != DBNull.Value ? Convert.ToInt32(row["AccountType"]) : 1;
                editBalance = Convert.ToDecimal(row["Balance"]);
                editCreditLimit = row["CreditLimit"] != DBNull.Value ? Convert.ToDecimal(row["CreditLimit"]) : 0m;
                editAvailableCredit = row["AvailableCredit"] != DBNull.Value ? Convert.ToDecimal(row["AvailableCredit"]) : 0m;
                editDueDate = row["DueDate"] != DBNull.Value ? Convert.ToInt32(row["DueDate"]) : 1;
                editMinimumPayment = row["MinimumPayment"] != DBNull.Value ? Convert.ToDecimal(row["MinimumPayment"]) : 0m;
                editInterestRate = row["InterestRate"] != DBNull.Value ? Convert.ToDecimal(row["InterestRate"]) : 0m;
                editWebUrl = row["WebUrl"]?.ToString() ?? string.Empty;
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

    private void SaveAccount()
    {
        try
        {
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfSaveAccount",
                new NamedValue("accountid", editAccountId),
                new NamedValue("userid", SiteInfoService.DefaultUserId),
                new NamedValue("accountname", editAccountName),
                new NamedValue("accountnumber", editAccountNumber),
                new NamedValue("accounttypeid", editAccountType),
                new NamedValue("balance", editBalance),
                new NamedValue("creditlimit", editCreditLimit),
                new NamedValue("availablecredit", editAvailableCredit),
                new NamedValue("duedate", editDueDate),
                new NamedValue("minimumpayment", editMinimumPayment),
                new NamedValue("interestrate", editInterestRate),
                new NamedValue("weburl", editWebUrl));

            currentView = ViewMode.List;
            LoadData();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task DeleteAccount()
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
            var dbName = SiteInfoService.DatabaseName;
            Sql.ExecuteNonQuery(dbName, "dbo", "spcfDeleteAccount", new NamedValue("accountid", editAccountId));
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

