using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace ClintonFrankland.Components.Pages;

public partial class CategoryBudgets
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private SiteInfoService SiteInfoService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private CategoryBudgetDataService CategoryBudgetData { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    private string errorMessage = string.Empty;
    private string editErrorMessage = string.Empty;
    private DateOnly selectedMonth = DateOnly.FromDateTime(DateTime.Today);
    private List<CategoryBudgetMonthRow> rows = new();
    private List<CategoryBudgetCategoryOption> categoryOptions = new();
    private int? editTargetId;
    private int editCategoryId;
    private decimal editPlannedAmount;

    private string SelectedMonthInput => selectedMonth.ToString("yyyy-MM");
    private string SelectedMonthLabel => selectedMonth.ToString("MMMM yyyy");
    private bool CanSave => editCategoryId > 0;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/category-budgets")}");
            return;
        }

        selectedMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task OnSelectedMonthChangedAsync(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString();
        if (string.IsNullOrWhiteSpace(rawValue) ||
            !DateOnly.TryParseExact($"{rawValue}-01", "yyyy-MM-dd", out var parsedMonth))
        {
            return;
        }

        selectedMonth = parsedMonth;
        ResetEditor();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            errorMessage = string.Empty;
            var userId = GetCurrentUserId();
            categoryOptions = await CategoryBudgetData.GetCategoryOptionsAsync(userId);
            rows = await CategoryBudgetData.GetMonthRowsAsync(userId, selectedMonth);

            if (editCategoryId == 0 && categoryOptions.Count > 0)
                editCategoryId = categoryOptions[0].CategoryId;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void EditRow(CategoryBudgetMonthRow row)
    {
        editErrorMessage = string.Empty;
        editTargetId = row.TargetId;
        editCategoryId = row.CategoryId;
        editPlannedAmount = row.PlannedAmount;
    }

    private async Task SaveTargetAsync()
    {
        editErrorMessage = string.Empty;

        if (editCategoryId <= 0)
        {
            editErrorMessage = "Choose a category.";
            return;
        }

        if (editPlannedAmount < 0)
        {
            editErrorMessage = "Budget amount cannot be negative.";
            return;
        }

        try
        {
            var request = new CategoryBudgetSaveRequest(
                editTargetId,
                editCategoryId,
                selectedMonth,
                editPlannedAmount);

            await CategoryBudgetData.SaveTargetAsync(GetCurrentUserId(), request);
            ResetEditor();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            editErrorMessage = $"Save failed: {ex.Message}";
        }
    }

    private async Task DeleteTargetAsync(CategoryBudgetMonthRow row)
    {
        if (!row.TargetId.HasValue)
            return;

        var confirmed = await DialogService.Confirm(
            $"Remove the {row.CategoryName} budget for {SelectedMonthLabel}?",
            "Confirm Delete",
            new ConfirmOptions
            {
                OkButtonText = "Delete",
                CancelButtonText = "Cancel",
                CloseDialogOnOverlayClick = true
            });

        if (confirmed != true)
            return;

        try
        {
            await CategoryBudgetData.DeleteTargetAsync(GetCurrentUserId(), row.TargetId.Value);
            ResetEditor();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void ResetEditor()
    {
        editErrorMessage = string.Empty;
        editTargetId = null;
        editCategoryId = categoryOptions.FirstOrDefault()?.CategoryId ?? 0;
        editPlannedAmount = 0m;
    }

    private int GetCurrentUserId() =>
        AuthService.CurrentUser.UserId > 0
            ? AuthService.CurrentUser.UserId
            : SiteInfoService.DefaultUserId;

    private string GetAlertCss(CategoryBudgetAlertStatus status) => status switch
    {
        CategoryBudgetAlertStatus.Overspent => "table-danger",
        CategoryBudgetAlertStatus.Warning => "table-warning",
        _ => string.Empty
    };

    private string GetAlertBadgeCss(CategoryBudgetAlertStatus status) => status switch
    {
        CategoryBudgetAlertStatus.Overspent => "bg-danger",
        CategoryBudgetAlertStatus.Warning => "bg-warning text-dark",
        _ => "bg-secondary"
    };

    private static string GetAlertText(CategoryBudgetAlertStatus status) => status switch
    {
        CategoryBudgetAlertStatus.Overspent => "Overspent",
        CategoryBudgetAlertStatus.Warning => "Warning",
        _ => "On track"
    };
}
