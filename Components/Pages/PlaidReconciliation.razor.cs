using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class PlaidReconciliation
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUser { get; set; } = default!;
    [Inject] private PlaidReconciliationService Reconciliation { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly List<PlaidReconciliationInboxItem> items = [];
    private readonly Dictionary<int, int?> selectedTransactionIds = [];
    private readonly Dictionary<int, AddToCheckbookDraft> addDrafts = [];
    private readonly HashSet<int> busyIds = [];
    private bool isLoading = true;
    private bool isError;
    private string message = string.Empty;

    private static readonly IReadOnlyList<InboxGroup> Groups =
    [
        new(PlaidReconciliationInboxGroup.Confident, "confident-matches", "Confident matches", "Unique, strong recommendations still require your confirmation."),
        new(PlaidReconciliationInboxGroup.ProbableOrAmbiguous, "needs-review", "Probable or ambiguous matches", "Choose the right eligible Checkbook entry, defer, or ignore."),
        new(PlaidReconciliationInboxGroup.Unmatched, "unmatched", "Unmatched", "No safe automatic recommendation was found."),
        new(PlaidReconciliationInboxGroup.Pending, "pending", "Pending", "Pending bank records are shown for awareness and are not eligible to clear."),
        new(PlaidReconciliationInboxGroup.ModifiedOrRemoved, "modified", "Modified or removed after confirmation", "Plaid changed its source evidence; existing ledger data is preserved for manual review.")
    ];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/accounts/plaid/reconciliation")}");
            return;
        }
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        items.Clear();
        selectedTransactionIds.Clear();
        addDrafts.Clear();
        items.AddRange(await Reconciliation.GetInboxAsync(CurrentUser.UserId, CancellationToken.None));
        foreach (var item in items)
        {
            selectedTransactionIds[item.PlaidTransactionStagingId] = item.RecommendedTransactionId;
            addDrafts[item.PlaidTransactionStagingId] = new AddToCheckbookDraft(item.TransactionDate,
                item.RuleSuggestion?.PayeeName ?? item.CleanedMerchant ?? item.BankDescription)
            {
                CategoryName = item.RuleSuggestion?.CategoryName ?? string.Empty,
                Notes = item.RuleSuggestion?.Notes
            };
        }
        isLoading = false;
    }

    private int? GetSelectedTransactionId(PlaidReconciliationInboxItem item) => selectedTransactionIds.GetValueOrDefault(item.PlaidTransactionStagingId);
    private void SelectTransaction(int stagingId, int transactionId) => selectedTransactionIds[stagingId] = transactionId;
    private bool IsBusy(PlaidReconciliationInboxItem item) => busyIds.Contains(item.PlaidTransactionStagingId);
    private AddToCheckbookDraft GetAddDraft(PlaidReconciliationInboxItem item) => addDrafts[item.PlaidTransactionStagingId];

    private async Task ConfirmAsync(PlaidReconciliationInboxItem item)
    {
        if (GetSelectedTransactionId(item) is not int transactionId) return;
        busyIds.Add(item.PlaidTransactionStagingId);
        var result = await Reconciliation.ConfirmAsync(CurrentUser.UserId, item.PlaidTransactionStagingId, transactionId, item.SourceFingerprint, CancellationToken.None);
        await FinishActionAsync(result);
    }

    private async Task SetReviewStateAsync(PlaidReconciliationInboxItem item, string reviewState)
    {
        busyIds.Add(item.PlaidTransactionStagingId);
        var result = await Reconciliation.SetReviewStateAsync(CurrentUser.UserId, item.PlaidTransactionStagingId, reviewState, item.SourceFingerprint, CancellationToken.None);
        await FinishActionAsync(result);
    }

    private async Task AddToCheckbookAsync(PlaidReconciliationInboxItem item)
    {
        busyIds.Add(item.PlaidTransactionStagingId);
        var draft = GetAddDraft(item);
        var result = await Reconciliation.AddToCheckbookAsync(CurrentUser.UserId, item.PlaidTransactionStagingId,
            new PlaidAddToCheckbookRequest(item.BudgetAccountId, draft.TransactionDate, draft.PayeeName, draft.CategoryName, draft.Notes, item.SourceFingerprint), CancellationToken.None);
        await FinishActionAsync(result);
    }

    private async Task FinishActionAsync(PlaidReconciliationActionResult result)
    {
        isError = result is not (PlaidReconciliationActionResult.Confirmed or PlaidReconciliationActionResult.AddedToCheckbook or PlaidReconciliationActionResult.Updated);
        message = result switch
        {
            PlaidReconciliationActionResult.Confirmed => "Match confirmed and the existing Checkbook entry was marked cleared.",
            PlaidReconciliationActionResult.AddedToCheckbook => "A cleared Checkbook entry was added and linked to this Plaid record.",
            PlaidReconciliationActionResult.Updated => "Review state saved.",
            PlaidReconciliationActionResult.RequiredFieldsMissing => "Payee, category, and date are required before adding to Checkbook.",
            PlaidReconciliationActionResult.Stale => "This bank record changed while you were reviewing it. Reloaded current evidence; no ledger entry was changed.",
            PlaidReconciliationActionResult.Unauthorized => "That Checkbook entry is not authorized for this Plaid account.",
            PlaidReconciliationActionResult.AlreadyReviewed => "This record or Checkbook entry was already linked or cleared by another action.",
            _ => "This candidate is no longer safe to confirm. No ledger entry was changed."
        };
        busyIds.Clear();
        await LoadAsync();
    }

    private void BackToConnections() => Navigation.NavigateTo("/accounts/plaid");
    private sealed record InboxGroup(PlaidReconciliationInboxGroup Group, string HeadingId, string Title, string Description);
    private sealed class AddToCheckbookDraft(DateOnly transactionDate, string payeeName)
    {
        public DateOnly TransactionDate { get; set; } = transactionDate;
        public string PayeeName { get; set; } = payeeName;
        public string CategoryName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
