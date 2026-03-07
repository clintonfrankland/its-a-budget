using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Components.Pages;

public partial class Profile
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ClintonFrankland.Data.ClintonFranklandDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private User? CurrentDbUser { get; set; }
    private string DisplayName { get; set; } = string.Empty;
    private string UserName { get; set; } = string.Empty;
    private string? EmailAddress { get; set; }
    private bool ListButtonsRight { get; set; } = true;
    private string NewPassword { get; set; } = string.Empty;

    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/profile")}");
            return;
        }

        await LoadProfileAsync();
        StateHasChanged();
    }

    private async Task LoadProfileAsync()
    {
        var current = AuthService.CurrentUser;
        CurrentDbUser = await DbContext.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.UserId == current.UserId);

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Unable to load current user profile.";
            return;
        }

        DisplayName = CurrentDbUser.DisplayName;
        UserName = CurrentDbUser.UserName;
        EmailAddress = CurrentDbUser.EmailAddress;
        ListButtonsRight = CurrentDbUser.ListButtonsRight;
    }

    private async Task SaveProfileAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Profile not loaded.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(UserName))
        {
            ErrorMessage = "Full name and username are required.";
            return;
        }

        var conflict = await DbContext.Users.AnyAsync(u =>
            !u.IsDeleted &&
            u.UserId != CurrentDbUser.UserId &&
            u.UserName.ToLower() == UserName.Trim().ToLower());

        if (conflict)
        {
            ErrorMessage = "That username is already in use.";
            return;
        }

        CurrentDbUser.DisplayName = DisplayName.Trim();
        CurrentDbUser.UserName = UserName.Trim();
        CurrentDbUser.EmailAddress = string.IsNullOrWhiteSpace(EmailAddress) ? null : EmailAddress.Trim();
        CurrentDbUser.ListButtonsRight = ListButtonsRight;

        await DbContext.SaveChangesAsync();
        await AuthService.RefreshCurrentUserAsync(CurrentDbUser);

        SuccessMessage = "Profile updated.";
    }

    private void GeneratePassword()
    {
        NewPassword = PasswordUtility.GenerateStrongPassword();
    }

    private async Task ResetPasswordAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Profile not loaded.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Enter or generate a password.";
            return;
        }

        CurrentDbUser.Salt = PasswordUtility.CreateSalt();
        CurrentDbUser.PasswordHash = PasswordUtility.HashPassword(NewPassword, CurrentDbUser.Salt);
        CurrentDbUser.PasswordResetRequestOn = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();

        NewPassword = string.Empty;
        SuccessMessage = "Password reset successful.";
    }
}
