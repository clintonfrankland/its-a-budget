using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Sharing
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUserContext { get; set; } = default!;
    [Inject] private BudgetInviteService InvitesService { get; set; } = default!;
    [Inject] private SharedBudgetDataService SharedBudgetsService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<SharedBudgetMembershipSummary> BudgetOptions { get; set; } = [];
    private List<BudgetMember> Members { get; set; } = [];
    private List<SharedBudgetInviteSummary> Invites { get; set; } = [];
    private Dictionary<int, BudgetMemberRole> MemberRoleSelections { get; } = [];
    private int SelectedSharedBudgetId { get; set; }
    private string InviteTarget { get; set; } = string.Empty;
    private BudgetMemberRole InviteRole { get; set; } = BudgetMemberRole.Viewer;
    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }
    private string? LastInviteLink { get; set; }
    private SharedBudgetMembershipSummary? SelectedBudget =>
        BudgetOptions.FirstOrDefault(b => b.SharedBudgetId == SelectedSharedBudgetId);
    private bool CanManageSelectedBudget =>
        SelectedBudget?.Role is BudgetMemberRole.Owner or BudgetMemberRole.Admin;
    private bool IsSelectedBudgetOwner => SelectedBudget?.Role == BudgetMemberRole.Owner;
    private bool IsPrivateSingleUserBudget =>
        CanManageSelectedBudget &&
        BudgetOptions.Count == 1 &&
        SelectedBudget?.ActiveMemberCount <= 1 &&
        Invites.Count == 0;

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
        BudgetOptions = await SharedBudgetsService.GetReadableSharedBudgetSummariesAsync(userId);

        if (SelectedSharedBudgetId <= 0 || BudgetOptions.All(b => b.SharedBudgetId != SelectedSharedBudgetId))
            SelectedSharedBudgetId = BudgetOptions.FirstOrDefault()?.SharedBudgetId ?? 0;

        Members = [];
        Invites = [];
        MemberRoleSelections.Clear();

        if (SelectedSharedBudgetId > 0 && CanManageSelectedBudget)
        {
            Members = await InvitesService.GetMembersAsync(userId, SelectedSharedBudgetId);
            Invites = await InvitesService.GetInvitesAsync(userId, SelectedSharedBudgetId);
            foreach (var member in Members)
                MemberRoleSelections[member.BudgetMemberId] = member.Role;
        }
    }

    private async Task SelectBudgetAsync(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var selectedId))
            SelectedSharedBudgetId = selectedId;

        SuccessMessage = null;
        ErrorMessage = null;
        LastInviteLink = null;
        await LoadAsync();
    }

    private void SetMemberRoleSelection(int budgetMemberId, ChangeEventArgs args)
    {
        if (Enum.TryParse<BudgetMemberRole>(args.Value?.ToString(), out var role))
            MemberRoleSelections[budgetMemberId] = role;
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

    private async Task ChangeMemberRoleAsync(int budgetMemberId)
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            if (MemberRoleSelections.TryGetValue(budgetMemberId, out var role) &&
                await SharedBudgetsService.ChangeMemberRoleAsync(CurrentUserContext.UserId, budgetMemberId, role))
            {
                SuccessMessage = "Member role updated.";
                await LoadAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task RemoveMemberAsync(int budgetMemberId)
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            if (await SharedBudgetsService.RemoveMemberAsync(CurrentUserContext.UserId, budgetMemberId))
            {
                SuccessMessage = "Member removed.";
                await LoadAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task TransferOwnershipAsync(int newOwnerMemberId)
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            if (await SharedBudgetsService.TransferOwnershipAsync(
                CurrentUserContext.UserId,
                SelectedSharedBudgetId,
                newOwnerMemberId))
            {
                SuccessMessage = "Ownership transferred.";
                await LoadAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task LeaveBudgetAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        LastInviteLink = null;

        try
        {
            if (await SharedBudgetsService.LeaveSharedBudgetAsync(CurrentUserContext.UserId, SelectedSharedBudgetId))
            {
                SuccessMessage = "You left the shared budget.";
                SelectedSharedBudgetId = 0;
                await LoadAsync();
            }
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
