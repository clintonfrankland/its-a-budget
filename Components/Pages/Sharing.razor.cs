using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Sharing
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUserContext { get; set; } = default!;
    [Inject] private BudgetInviteService InvitesService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<SharedBudget> ManageableBudgets { get; set; } = [];
    private List<BudgetMember> Members { get; set; } = [];
    private List<SharedBudgetInviteSummary> Invites { get; set; } = [];
    private int SelectedSharedBudgetId { get; set; }
    private string InviteTarget { get; set; } = string.Empty;
    private BudgetMemberRole InviteRole { get; set; } = BudgetMemberRole.Viewer;
    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }
    private string? LastInviteLink { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/sharing")}");
            return;
        }

        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        var userId = CurrentUserContext.UserId;
        ManageableBudgets = await InvitesService.GetManageableSharedBudgetsAsync(userId);

        if (SelectedSharedBudgetId <= 0 && ManageableBudgets.Count > 0)
            SelectedSharedBudgetId = ManageableBudgets[0].SharedBudgetId;

        if (SelectedSharedBudgetId > 0)
        {
            Members = await InvitesService.GetMembersAsync(userId, SelectedSharedBudgetId);
            Invites = await InvitesService.GetInvitesAsync(userId, SelectedSharedBudgetId);
        }
    }

    private async Task CreateInviteAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            var result = await InvitesService.CreateInviteAsync(
                CurrentUserContext.UserId,
                SelectedSharedBudgetId,
                InviteTarget,
                InviteRole);
            LastInviteLink = BuildAcceptLink(result.PlainToken);
            SuccessMessage = "Invite created.";
            InviteTarget = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task ResendInviteAsync(int inviteId)
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            var result = await InvitesService.ResendInviteAsync(CurrentUserContext.UserId, inviteId);
            LastInviteLink = BuildAcceptLink(result.PlainToken);
            SuccessMessage = "Invite resent.";
            await LoadAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task RevokeInviteAsync(int inviteId)
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            if (await InvitesService.RevokeInviteAsync(CurrentUserContext.UserId, inviteId))
            {
                SuccessMessage = "Invite revoked.";
                await LoadAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private string BuildAcceptLink(string token) =>
        Navigation.ToAbsoluteUri($"/share/accept/{Uri.EscapeDataString(token)}").ToString();

    private static string InviteStatus(SharedBudgetInviteSummary invite)
    {
        if (invite.AcceptedAtUtc.HasValue)
            return "Accepted";

        if (invite.RevokedAtUtc.HasValue)
            return "Revoked";

        return invite.ExpiresAtUtc <= DateTime.UtcNow ? "Expired" : "Pending";
    }
}
