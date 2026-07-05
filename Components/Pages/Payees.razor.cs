using ClintonFrankland.Models.ViewModels;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;

namespace ClintonFrankland.Components.Pages;

public partial class Payees
{
    [Inject]
    private AuthService AuthService { get; set; } = default!;

    [Inject]
    private CurrentUserContext CurrentUser { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private PayeesDataService PayeesData { get; set; } = default!;

    private enum ViewMode { List, Edit, Merge }
    private ViewMode currentView = ViewMode.List;

    private string errorMessage = string.Empty;
    private string editErrorMessage = string.Empty;
    private string mergeErrorMessage = string.Empty;
    private string searchText = string.Empty;
    private bool includeDeleted = false;
    private int editPayeeId = -1;
    private string editPayeeName = string.Empty;
    private int keepPayeeId;
    private string mergeConfirmationName = string.Empty;
    private PayeeMergePreview? mergePreview;
    private RadzenDataGrid<PayeeSummaryViewModel>? payeesGrid;
    private List<PayeeSummaryViewModel> payees = new();
    private List<PayeeSummaryViewModel> activePayees = new();
    private readonly HashSet<int> selectedPayeeIds = new();

    private int CurrentPayeesUserId => CurrentUser.UserId;

    private IEnumerable<PayeeSummaryViewModel> filteredPayees => FilterPayees();
    private List<PayeeSummaryViewModel> selectedPayees => activePayees
        .Where(p => selectedPayeeIds.Contains(p.PayeeId))
        .OrderBy(p => p.PayeeName)
        .ToList();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/payees")}");
            return;
        }

        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            errorMessage = string.Empty;
            payees = await PayeesData.GetPayeeSummariesAsync(CurrentPayeesUserId, includeDeleted, DateOnly.FromDateTime(DateTime.Today));
            activePayees = payees.Where(p => !p.IsDeleted).OrderBy(p => p.PayeeName).ToList();
            selectedPayeeIds.IntersectWith(activePayees.Select(p => p.PayeeId));
            keepPayeeId = selectedPayees.FirstOrDefault()?.PayeeId ?? 0;
            ClearMergePreview();
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private async Task ShowEditAsync(int payeeId)
    {
        try
        {
            editErrorMessage = string.Empty;
            var payee = await PayeesData.GetPayeeForEditAsync(CurrentPayeesUserId, payeeId);
            if (payee is null)
            {
                errorMessage = "Payee was not found.";
                return;
            }

            editPayeeId = payee.PayeeId;
            editPayeeName = payee.PayeeName;
            currentView = ViewMode.Edit;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType()}: {ex.Message}";
        }
    }

    private void ShowMerge()
    {
        mergeErrorMessage = string.Empty;
        ClearMergePreview();
        if (selectedPayeeIds.Count < 2)
        {
            mergeErrorMessage = "Select at least two active payees to merge.";
            return;
        }

        keepPayeeId = selectedPayees.First().PayeeId;
        currentView = ViewMode.Merge;
    }

    private async Task SavePayeeAsync()
    {
        try
        {
            editErrorMessage = string.Empty;
            await PayeesData.UpdatePayeeAsync(CurrentPayeesUserId, editPayeeId, editPayeeName);
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            editErrorMessage = ex.Message;
        }
    }

    private async Task PreviewMergeAsync()
    {
        try
        {
            mergeErrorMessage = string.Empty;
            mergeConfirmationName = string.Empty;
            mergePreview = await PayeesData.PreviewMergeAsync(CurrentPayeesUserId, keepPayeeId, selectedPayeeIds);
        }
        catch (Exception ex)
        {
            mergePreview = null;
            mergeErrorMessage = ex.Message;
        }
    }

    private async Task MergePayeesAsync()
    {
        try
        {
            mergeErrorMessage = string.Empty;
            await PayeesData.MergePayeesAsync(CurrentPayeesUserId, keepPayeeId, selectedPayeeIds, mergeConfirmationName);
            selectedPayeeIds.Clear();
            currentView = ViewMode.List;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            mergeErrorMessage = ex.Message;
        }
    }

    private void CancelEdit()
    {
        editErrorMessage = string.Empty;
        mergeErrorMessage = string.Empty;
        currentView = ViewMode.List;
    }

    private void ClearMergePreview()
    {
        mergePreview = null;
        mergeConfirmationName = string.Empty;
    }

    private void TogglePayeeSelection(int payeeId, bool selected)
    {
        if (!activePayees.Any(p => p.PayeeId == payeeId))
            return;

        if (selected)
            selectedPayeeIds.Add(payeeId);
        else
            selectedPayeeIds.Remove(payeeId);

        if (!selectedPayeeIds.Contains(keepPayeeId))
            keepPayeeId = selectedPayees.FirstOrDefault()?.PayeeId ?? 0;

        ClearMergePreview();
    }

    private void OpenTransactions(string payeeName)
    {
        Navigation.NavigateTo($"/checkbook?search={Uri.EscapeDataString(payeeName)}");
    }

    private void OnSearch(string? value)
    {
        searchText = value ?? string.Empty;
        payeesGrid?.GoToPage(0);
    }

    private IEnumerable<PayeeSummaryViewModel> FilterPayees()
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return payees;

        var searchLower = searchText.ToLower();
        return payees.Where(item =>
            item.PayeeName.ToLower().Contains(searchLower) ||
            item.BudgetCount.ToString().Contains(searchLower) ||
            item.LastTransactionDate?.ToString("MM/dd/yyyy").Contains(searchLower) == true ||
            item.DuplicateHint.ToLower().Contains(searchLower));
    }

}
