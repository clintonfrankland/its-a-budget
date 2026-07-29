using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class AcceptShareInvite
{
    [Parameter] public string Token { get; set; } = string.Empty;

    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private CurrentUserContext CurrentUserContext { get; set; } = default!;
    [Inject] private BudgetInviteService InvitesService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private BudgetInvitePreview? Preview { get; set; }
    private string? Message { get; set; }
    private string MessageCss { get; set; } = "alert-info";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri).Insert(0, "/"))}");
            return;
        }

        Preview = await InvitesService.PreviewInviteAsync(Token);
        if (Preview.Status != BudgetInviteAcceptStatus.Accepted)
            SetMessage(Preview.Status);

        StateHasChanged();
    }

    private async Task AcceptAsync()
    {
        var result = await InvitesService.AcceptInviteAsync(Token, CurrentUserContext.UserId);
        SetMessage(result.Status);
        Preview = null;
    }

    private void SetMessage(BudgetInviteAcceptStatus status)
    {
        MessageCss = status == BudgetInviteAcceptStatus.Accepted || status == BudgetInviteAcceptStatus.AlreadyAccepted
            ? "alert-success"
            : "alert-warning";

        Message = status switch
        {
            BudgetInviteAcceptStatus.Accepted => "Budget invite accepted.",
            BudgetInviteAcceptStatus.AlreadyAccepted => "This invite has already been accepted.",
            BudgetInviteAcceptStatus.Expired => "This invite has expired. Ask the budget owner or admin to resend it.",
            BudgetInviteAcceptStatus.Revoked => "This invite has been revoked.",
            BudgetInviteAcceptStatus.WrongUser => "This invite is for a different It's a Budget user.",
            BudgetInviteAcceptStatus.AuthenticationRequired => "Sign in before accepting this invite.",
            _ => "This invite link is not valid."
        };
    }
}
