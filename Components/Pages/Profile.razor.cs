using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace ClintonFrankland.Components.Pages;

public partial class Profile
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ClintonFrankland.Data.ClintonFranklandDbContext DbContext { get; set; } = default!;
    [Inject] private ExternalIdentityLinkService ExternalIdentityLinks { get; set; } = default!;
    [Inject] private WeeklyUpcomingBillsDigestService WeeklyUpcomingBillsDigestService { get; set; } = default!;
    [Inject] private IOptions<AuthentikOidcOptions> AuthentikOptions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private User? CurrentDbUser { get; set; }
    private string DisplayName { get; set; } = string.Empty;
    private string UserName { get; set; } = string.Empty;
    private string? EmailAddress { get; set; }
    private bool ListButtonsRight { get; set; } = true;
    private string NewPassword { get; set; } = string.Empty;

    // Notification preferences
    private bool ReceiveBillDueNotices { get; set; }
    private bool ReceiveWeeklyUpcomingBillDigest { get; set; }
    private string NotificationTimezone { get; set; } = "America/New_York";
    private TimeOnly NotificationDeliveryTime { get; set; } = new TimeOnly(8, 0);
    private bool IsSendingWeeklyPreview { get; set; }

    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }
    private bool IsAuthentikEnabled => AuthentikOptions.Value.IsUsable;
    private bool HasExternalIdentity => CurrentDbUser is not null && ExternalIdentityLinkService.HasExternalIdentity(CurrentDbUser);
    private string AuthentikLinkUrl => AuthentikLoginLinks.BuildLinkUrl(AuthentikOptions.Value);
    private string ExternalIdentityStatus =>
        HasExternalIdentity
            ? $"{CurrentDbUser!.ExternalProvider} account linked"
            : "No Authentik account linked";

    // Available timezones for dropdown
    private static readonly List<TimezoneOption> AvailableTimezones = GetAvailableTimezones();

    private record TimezoneOption(string Id, string DisplayName);

    private static List<TimezoneOption> GetAvailableTimezones()
    {
        var timezones = new List<TimezoneOption>();

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            // Try to get IANA ID, fall back to Windows ID
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId))
            {
                timezones.Add(new TimezoneOption(ianaId, $"{tz.DisplayName}"));
            }
            else
            {
                timezones.Add(new TimezoneOption(tz.Id, $"{tz.DisplayName}"));
            }
        }

        return timezones.DistinctBy(t => t.Id).OrderBy(t => t.DisplayName).ToList();
    }

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

        // Load notification preferences
        ReceiveBillDueNotices = CurrentDbUser.ReceiveBillDueNotices;
        ReceiveWeeklyUpcomingBillDigest = CurrentDbUser.ReceiveWeeklyUpcomingBillDigest;
        NotificationTimezone = CurrentDbUser.NotificationTimezone;
        NotificationDeliveryTime = CurrentDbUser.NotificationDeliveryTime;
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

    private async Task SaveNotificationPreferencesAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Profile not loaded.";
            return;
        }

        // Validate timezone
        if (!IsValidTimezone(NotificationTimezone))
        {
            ErrorMessage = "Invalid timezone selected.";
            return;
        }

        // Warn if enabling notifications without email
        if ((ReceiveBillDueNotices || ReceiveWeeklyUpcomingBillDigest) && string.IsNullOrWhiteSpace(EmailAddress))
        {
            ErrorMessage = "Please set an email address before enabling email notifications.";
            return;
        }

        CurrentDbUser.ReceiveBillDueNotices = ReceiveBillDueNotices;
        CurrentDbUser.ReceiveWeeklyUpcomingBillDigest = ReceiveWeeklyUpcomingBillDigest;
        CurrentDbUser.EmailAddress = string.IsNullOrWhiteSpace(EmailAddress) ? null : EmailAddress.Trim();
        CurrentDbUser.NotificationTimezone = NotificationTimezone;
        CurrentDbUser.NotificationDeliveryTime = NotificationDeliveryTime;

        await DbContext.SaveChangesAsync();
        await AuthService.RefreshCurrentUserAsync(CurrentDbUser);

        SuccessMessage = "Notification preferences updated.";
    }

    private async Task SendWeeklyDigestPreviewAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Profile not loaded.";
            return;
        }

        IsSendingWeeklyPreview = true;

        try
        {
            await SaveNotificationPreferencesAsync();
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
                return;

            var result = await WeeklyUpcomingBillsDigestService.SendManualPreviewAsync(CurrentDbUser.UserId, CancellationToken.None);
            if (result.Sent)
            {
                SuccessMessage = $"{result.Message} Bills included: {result.UpcomingBillCount}.";
            }
            else if (result.Skipped)
            {
                SuccessMessage = result.Message;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            IsSendingWeeklyPreview = false;
        }
    }

    private static bool IsValidTimezone(string timezoneId)
    {
        // Check if it's a valid IANA or Windows timezone
        try
        {
            // Try IANA first
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timezoneId, out var windowsId))
            {
                TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }

            // Try as Windows ID directly
            TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
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

    private async Task UnlinkAuthentikAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (CurrentDbUser is null)
        {
            ErrorMessage = "Profile not loaded.";
            return;
        }

        if (!HasExternalIdentity)
        {
            ErrorMessage = "No Authentik account is linked.";
            return;
        }

        var confirmed = await JS.InvokeAsync<bool>(
            "confirm",
            $"Unlink Authentik account from {CurrentDbUser.UserName}?");
        if (!confirmed)
            return;

        try
        {
            CurrentDbUser = await ExternalIdentityLinks.UnlinkExternalIdentityForCurrentUserAsync(AuthService.CurrentUser);
            await AuthService.RefreshCurrentUserAsync(CurrentDbUser);
            SuccessMessage = "Authentik account unlinked.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
