using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Onboarding
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUser { get; set; } = default!;
    [Inject] private OnboardingService OnboardingData { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool loading = true;
    private bool saving;
    private int step;
    private int? selectedBudgetId;
    private string budgetName = "My Budget";
    private string accountName = "Checking";
    private int accountTypeId = 1;
    private decimal openingBalance;
    private string categoryText = "Groceries\nUtilities\nTransportation";
    private string inviteTarget = string.Empty;
    private string? inviteLink;
    private string? errorMessage;
    private string? successMessage;
    private List<SharedBudgetMembershipSummary> availableBudgets = [];
    private List<Account> accounts = [];
    private int ProgressPercent => (int)Math.Round((step + 1) / 6m * 100m);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/onboarding")}");
            return;
        }
        await ReloadAsync();
        loading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadAsync()
    {
        var state = await OnboardingData.GetStateAsync(CurrentUser.UserId);
        if (state is null) { Navigation.NavigateTo("/"); return; }
        if (state.IsCompleted) { Navigation.NavigateTo("/"); return; }
        step = state.Step;
        selectedBudgetId = state.SharedBudgetId;
        if (!string.IsNullOrWhiteSpace(state.BudgetName)) budgetName = state.BudgetName;
        availableBudgets = state.AvailableBudgets.ToList();
        accounts = state.Accounts.ToList();
    }

    private async Task RunSaveAsync(Func<Task> action)
    {
        saving = true; errorMessage = null; successMessage = null;
        try { await action(); await ReloadAsync(); }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        { errorMessage = exception.Message; }
        catch { errorMessage = "We could not save that step. Your earlier progress is safe. Please try again."; }
        finally { saving = false; }
    }

    private Task MoveToAsync(int nextStep) => RunSaveAsync(() => OnboardingData.SaveStepAsync(CurrentUser.UserId, nextStep));
    private Task GoBackAsync() => MoveToAsync(Math.Max(OnboardingService.WelcomeStep, step - 1));
    private Task SaveBudgetAsync() => RunSaveAsync(async () => await OnboardingData.SaveBudgetAsync(CurrentUser.UserId, selectedBudgetId, budgetName));
    private Task SaveAccountAsync() => RunSaveAsync(async () => await OnboardingData.SaveAccountAsync(CurrentUser.UserId, accountName, accountTypeId, openingBalance));
    private Task SaveCategoriesAsync() => RunSaveAsync(() => OnboardingData.SaveCategoriesAsync(CurrentUser.UserId, categoryText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    private Task SaveInviteAsync() => RunSaveAsync(async () =>
    {
        var result = await OnboardingData.SaveInviteAsync(CurrentUser.UserId, inviteTarget);
        inviteLink = Navigation.ToAbsoluteUri($"/share/accept/{Uri.EscapeDataString(result.PlainToken)}").ToString();
        successMessage = "Invite created.";
    });
    private Task CompleteAsync() => RunSaveAsync(async () =>
    {
        await OnboardingData.CompleteAsync(CurrentUser.UserId);
        Navigation.NavigateTo("/", forceLoad: true);
    });
}
