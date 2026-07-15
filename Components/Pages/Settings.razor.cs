using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace ClintonFrankland.Components.Pages;

public partial class Settings
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ClintonFrankland.Data.ClintonFranklandDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private EmailSenderService EmailSenderService { get; set; } = default!;
    [Inject] private ExternalIdentityLinkService ExternalIdentityLinks { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // SMTP Settings
    private bool IsEnabled { get; set; }
    private string? SenderEmail { get; set; }
    private string? FromName { get; set; }
    private SmtpTlsMode TlsMode { get; set; } = SmtpTlsMode.StartTls;
    private bool AllowInvalidCerts { get; set; }
    private string? TestRecipientEmail { get; set; }

    private string ServerHostDisplay { get; set; } = "";
    private string ServerPortDisplay { get; set; } = "";
    private string ServerUserDisplay { get; set; } = "";

    private string? Message { get; set; }
    private string MessageCss { get; set; } = "alert-info";

    private bool IsSendingTest { get; set; }
    private bool ShowTestLog { get; set; }
    private List<string> TestLogLines { get; set; } = [];

    // Bill Due Notification Settings
    private bool BillDueIsEnabled { get; set; }
    private int BillDueSoonDays { get; set; } = 3;
    private bool BillPastDueEnabled { get; set; } = true;
    private int BillPastDueMaxDays { get; set; } = 30;

    // User Management
    private List<User> UsersList { get; set; } = [];
    private string? UserErrorMessage { get; set; }

    private User? EditingUser { get; set; }
    private bool IsNewUser { get; set; }
    private string FormDisplayName { get; set; } = "";
    private string FormUserName { get; set; } = "";
    private string? FormEmail { get; set; }
    private bool FormIsAdmin { get; set; }

    private User? PasswordUser { get; set; }
    private string PasswordValue { get; set; } = "";

    private User? LinkUser { get; set; }
    private string LinkProvider { get; set; } = "authentik";
    private string LinkSubject { get; set; } = "";
    private string? LinkEmail { get; set; }
    private string? LinkDisplayName { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await AuthService.InitializeAsync();
        if (!AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo($"/login?Return={Uri.EscapeDataString("/settings")}");
            return;
        }

        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        if (AuthService.CurrentUser.IsAdmin)
        {
            await LoadSmtpSettingsAsync();
            await LoadBillDueSettingsAsync();
        }

        await LoadUsersAsync();
    }

    #region SMTP Settings

    private async Task LoadSmtpSettingsAsync()
    {
        var smtp = await DbContext.SmtpSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (smtp is null)
        {
            smtp = new SmtpSetting { Id = 1, UpdatedAtUtc = DateTime.UtcNow };
            DbContext.SmtpSettings.Add(smtp);
            await DbContext.SaveChangesAsync();
        }

        IsEnabled = smtp.IsEnabled;
        SenderEmail = smtp.SenderEmail;
        FromName = smtp.FromName;
        TlsMode = smtp.TlsMode;
        AllowInvalidCerts = smtp.AllowInvalidCerts;
        TestRecipientEmail = smtp.TestRecipientEmail;

        var server = EmailSenderService.GetServerConfig();
        ServerHostDisplay = string.IsNullOrWhiteSpace(server.Host) ? "(not set)" : server.Host;
        ServerPortDisplay = server.Port <= 0 ? "587" : server.Port.ToString();
        ServerUserDisplay = string.IsNullOrWhiteSpace(server.UserName) ? "(not set)" : server.UserName;
    }

    private async Task PersistSmtpAsync()
    {
        var smtp = await DbContext.SmtpSettings.FirstAsync(x => x.Id == 1);
        smtp.IsEnabled = IsEnabled;
        smtp.SenderEmail = string.IsNullOrWhiteSpace(SenderEmail) ? null : SenderEmail.Trim();
        smtp.FromName = string.IsNullOrWhiteSpace(FromName) ? null : FromName.Trim();
        smtp.TlsMode = TlsMode;
        smtp.AllowInvalidCerts = AllowInvalidCerts;
        smtp.TestRecipientEmail = string.IsNullOrWhiteSpace(TestRecipientEmail) ? null : TestRecipientEmail.Trim();
        smtp.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();
        Message = "SMTP settings saved.";
        MessageCss = "alert-success";
    }

    private async Task OnEnabledChanged(ChangeEventArgs e)
    {
        IsEnabled = e.Value is bool b && b;
        await PersistSmtpAsync();
    }

    private async Task OnSenderChanged(ChangeEventArgs e)
    {
        SenderEmail = e.Value?.ToString();
        await PersistSmtpAsync();
    }

    private async Task OnFromNameChanged(ChangeEventArgs e)
    {
        FromName = e.Value?.ToString();
        await PersistSmtpAsync();
    }

    private async Task OnTlsModeChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (Enum.TryParse<SmtpTlsMode>(raw, ignoreCase: true, out var mode))
            TlsMode = mode;

        await PersistSmtpAsync();
    }

    private async Task OnAllowInvalidCertsChanged(ChangeEventArgs e)
    {
        AllowInvalidCerts = e.Value is bool b && b;
        await PersistSmtpAsync();
    }

    private async Task OnTestRecipientChanged(ChangeEventArgs e)
    {
        TestRecipientEmail = e.Value?.ToString();
        await PersistSmtpAsync();
    }

    private void ClearTestLog()
    {
        TestLogLines.Clear();
        ShowTestLog = false;
    }

    private async Task AppendTestLogAsync(string line)
    {
        TestLogLines.Add(line);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task SendTestAsync()
    {
        if (IsSendingTest) return;

        IsSendingTest = true;
        TestLogLines.Clear();
        ShowTestLog = true;
        Message = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            await AppendTestLogAsync("Starting SMTP test...");
            await PersistSmtpAsync();

            var target = !string.IsNullOrWhiteSpace(TestRecipientEmail)
                ? TestRecipientEmail
                : (AuthService.CurrentUser.EmailAddress ?? SenderEmail);

            if (string.IsNullOrWhiteSpace(target))
            {
                await AppendTestLogAsync("No test recipient configured.");
                Message = "Set a Test Recipient Email, your profile email, or the Sender Email before sending a test.";
                MessageCss = "alert-warning";
                return;
            }

            await EmailSenderService.SendWithDiagnosticsAsync(
                target,
                "Budget App SMTP Test",
                "SMTP configuration is working.",
                log: msg => _ = AppendTestLogAsync(msg));

            Message = $"Test email sent to {target}.";
            MessageCss = "alert-success";
            await AppendTestLogAsync("SUCCESS: Test email sent.");
        }
        catch (Exception ex)
        {
            Message = $"Failed to send test email: {ex.Message}";
            MessageCss = "alert-danger";
            await AppendTestLogAsync($"ERROR: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsSendingTest = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Bill Due Notifications

    private async Task LoadBillDueSettingsAsync()
    {
        var s = await DbContext.BillDueNotificationSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null)
        {
            s = new BillDueNotificationSetting { Id = 1, UpdatedAtUtc = DateTime.UtcNow };
            DbContext.BillDueNotificationSettings.Add(s);
            await DbContext.SaveChangesAsync();
        }

        BillDueIsEnabled = s.IsEnabled;
        BillDueSoonDays = s.DueSoonDays;
        BillPastDueEnabled = s.PastDueEnabled;
        BillPastDueMaxDays = s.PastDueMaxDays;
    }

    private async Task PersistBillDueAsync()
    {
        var s = await DbContext.BillDueNotificationSettings.FirstAsync(x => x.Id == 1);
        s.IsEnabled = BillDueIsEnabled;
        s.DueSoonDays = Math.Clamp(BillDueSoonDays, 0, 60);
        s.PastDueEnabled = BillPastDueEnabled;
        s.PastDueMaxDays = Math.Clamp(BillPastDueMaxDays, 0, 365);
        s.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();
        Message = "Bill-due notification settings saved.";
        MessageCss = "alert-success";
    }

    private async Task OnBillDueEnabledChanged(ChangeEventArgs e)
    {
        BillDueIsEnabled = e.Value is bool b && b;
        await PersistBillDueAsync();
    }

    private async Task OnBillDueSoonDaysChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n))
            BillDueSoonDays = n;
        await PersistBillDueAsync();
    }

    private async Task OnBillPastDueEnabledChanged(ChangeEventArgs e)
    {
        BillPastDueEnabled = e.Value is bool b && b;
        await PersistBillDueAsync();
    }

    private async Task OnBillPastDueMaxDaysChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n))
            BillPastDueMaxDays = n;
        await PersistBillDueAsync();
    }

    private async Task ScrollToBillDue()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('bill-due')?.scrollIntoView({behavior:'smooth'});");
    }

    #endregion

    #region User Management

    private async Task LoadUsersAsync()
    {
        UsersList = await ExternalIdentityLinks.GetVisibleUsersAsync(AuthService.CurrentUser);
    }

    private void ShowNewUser()
    {
        if (!AuthService.CurrentUser.IsAdmin)
        {
            UserErrorMessage = "Only admins can create users.";
            return;
        }

        IsNewUser = true;
        EditingUser = new User();
        FormDisplayName = "";
        FormUserName = "";
        FormEmail = "";
        FormIsAdmin = false;
    }

    private void EditUser(User user)
    {
        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != user.UserId)
        {
            UserErrorMessage = "You can only edit your own user profile.";
            return;
        }

        IsNewUser = false;
        EditingUser = user;
        FormDisplayName = user.DisplayName;
        FormUserName = user.UserName;
        FormEmail = user.EmailAddress;
        FormIsAdmin = user.IsAdmin;
    }

    private async Task SaveUser()
    {
        if (EditingUser is null) return;
        var current = AuthService.CurrentUser;

        if (!current.IsAdmin && !IsNewUser && current.UserId != EditingUser.UserId)
        {
            UserErrorMessage = "You can only edit your own user profile.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormDisplayName) || string.IsNullOrWhiteSpace(FormUserName))
        {
            UserErrorMessage = "Full name and username are required.";
            return;
        }

        int? createdUserId = null;
        if (IsNewUser)
        {
            var nextId = await DbContext.Users.Select(u => (int?)u.UserId).MaxAsync() ?? 0;
            var salt = PasswordUtility.CreateSalt();
            var tempPassword = PasswordUtility.GenerateStrongPassword();

            var user = new User
            {
                UserId = nextId + 1,
                SiteId = current.SiteId == 0 ? 1 : current.SiteId,
                DisplayName = FormDisplayName.Trim(),
                UserName = FormUserName.Trim(),
                EmailAddress = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim(),
                IsAdmin = current.IsAdmin && FormIsAdmin,
                IsDeleted = false,
                Salt = salt,
                PasswordHash = PasswordUtility.HashPassword(tempPassword, salt),
                FirstLogin = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };

            DbContext.Users.Add(user);
            createdUserId = user.UserId;
            Message = $"User created. Temporary password: {tempPassword}";
            MessageCss = "alert-info";
        }
        else
        {
            var dbUser = await DbContext.Users.FirstAsync(u => u.UserId == EditingUser.UserId);
            dbUser.DisplayName = FormDisplayName.Trim();
            dbUser.UserName = FormUserName.Trim();
            dbUser.EmailAddress = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim();

            if (current.IsAdmin)
            {
                dbUser.IsAdmin = FormIsAdmin;
            }

            Message = "User updated.";
            MessageCss = "alert-success";
        }

        await DbContext.SaveChangesAsync();
        if (createdUserId.HasValue)
            await new SharedBudgetDataService(DbContext).GetDefaultSharedBudgetIdAsync(createdUserId.Value);
        CloseEditModal();
        await LoadUsersAsync();
        UserErrorMessage = null;
    }

    private void OpenPasswordUtility(User user)
    {
        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != user.UserId)
        {
            UserErrorMessage = "You can only reset your own password.";
            return;
        }

        if (HasExternalIdentity(user))
        {
            UserErrorMessage = "Password utility is local-account-only. Unlink the Authentik identity before resetting this user's local password.";
            return;
        }

        PasswordUser = user;
        PasswordValue = "";
    }

    private void GeneratePassword()
    {
        PasswordValue = PasswordUtility.GenerateStrongPassword();
    }

    private async Task ResetPassword()
    {
        if (PasswordUser is null || string.IsNullOrWhiteSpace(PasswordValue))
        {
            UserErrorMessage = "Enter or generate a password first.";
            return;
        }

        var current = AuthService.CurrentUser;
        if (!current.IsAdmin && current.UserId != PasswordUser.UserId)
        {
            UserErrorMessage = "You can only reset your own password.";
            return;
        }

        var dbUser = await DbContext.Users.FirstAsync(u => u.UserId == PasswordUser.UserId);
        if (HasExternalIdentity(dbUser))
        {
            UserErrorMessage = "Password utility is local-account-only. Unlink the Authentik identity before resetting this user's local password.";
            return;
        }

        dbUser.Salt = PasswordUtility.CreateSalt();
        dbUser.PasswordHash = PasswordUtility.HashPassword(PasswordValue, dbUser.Salt);
        dbUser.PasswordResetRequestOn = DateTime.UtcNow;

        var passwordUserName = dbUser.UserName;

        await DbContext.SaveChangesAsync();
        ClosePasswordModal();
        UserErrorMessage = null;
        Message = $"Password reset for {passwordUserName}.";
        MessageCss = "alert-success";
    }

    private void CloseEditModal()
    {
        EditingUser = null;
        IsNewUser = false;
    }

    private void ClosePasswordModal()
    {
        PasswordUser = null;
        PasswordValue = "";
    }

    private void OpenExternalIdentityModal(User user)
    {
        if (!AuthService.CurrentUser.IsAdmin)
        {
            UserErrorMessage = "Only admins can link Authentik identities.";
            return;
        }

        LinkUser = user;
        LinkProvider = string.IsNullOrWhiteSpace(user.ExternalProvider) ? "authentik" : user.ExternalProvider;
        LinkSubject = user.ExternalSubject ?? "";
        LinkEmail = user.ExternalEmail;
        LinkDisplayName = user.ExternalDisplayName;
        UserErrorMessage = null;
    }

    private async Task SaveExternalIdentity()
    {
        if (LinkUser is null)
            return;

        if (!AuthService.CurrentUser.IsAdmin)
        {
            UserErrorMessage = "Only admins can link Authentik identities.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LinkProvider) || string.IsNullOrWhiteSpace(LinkSubject))
        {
            UserErrorMessage = "Authentik provider and subject are required.";
            return;
        }

        var wasLinked = HasExternalIdentity(LinkUser);
        var confirmationMessage = wasLinked
            ? $"Relink {LinkUser.UserName} to this Authentik subject?"
            : $"Link {LinkUser.UserName} to this Authentik subject?";
        var confirmed = await JS.InvokeAsync<bool>("confirm", confirmationMessage);
        if (!confirmed)
            return;

        try
        {
            await ExternalIdentityLinks.LinkExternalIdentityAsAdminAsync(
                AuthService.CurrentUser,
                LinkUser.UserId,
                new ExternalIdentityProfile(LinkProvider, LinkSubject, LinkEmail, LinkDisplayName));

            CloseExternalIdentityModal();
            await LoadUsersAsync();
            Message = wasLinked ? "Authentik identity relinked." : "Authentik identity linked.";
            MessageCss = "alert-success";
            UserErrorMessage = null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            UserErrorMessage = ex.Message;
        }
    }

    private async Task UnlinkExternalIdentity(User user)
    {
        if (!AuthService.CurrentUser.IsAdmin)
        {
            UserErrorMessage = "Only admins can unlink Authentik identities.";
            return;
        }

        if (!HasExternalIdentity(user))
        {
            UserErrorMessage = "This user does not have a linked Authentik identity.";
            return;
        }

        var confirmed = await JS.InvokeAsync<bool>(
            "confirm",
            $"Unlink Authentik identity from {user.UserName}?");
        if (!confirmed)
            return;

        try
        {
            await ExternalIdentityLinks.UnlinkExternalIdentityAsAdminAsync(AuthService.CurrentUser, user.UserId);
            await LoadUsersAsync();
            Message = $"Authentik identity unlinked from {user.UserName}.";
            MessageCss = "alert-success";
            UserErrorMessage = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            UserErrorMessage = ex.Message;
        }
    }

    private void CloseExternalIdentityModal()
    {
        LinkUser = null;
        LinkProvider = "authentik";
        LinkSubject = "";
        LinkEmail = null;
        LinkDisplayName = null;
    }

    private static bool HasExternalIdentity(User user) => ExternalIdentityLinkService.HasExternalIdentity(user);

    private static string ExternalIdentityStatus(User user) =>
        HasExternalIdentity(user)
            ? $"{user.ExternalProvider} linked"
            : "Not linked";

    private static string ExternalIdentitySubjectStatus(User user) =>
        string.IsNullOrWhiteSpace(user.ExternalSubject) ? "No subject" : "Subject present";

    private static string ExternalIdentityMetadata(User user)
    {
        var parts = new[] { user.ExternalDisplayName, user.ExternalEmail }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? "No provider profile details" : string.Join(" / ", parts);
    }

    private static string LastExternalLoginText(User user) =>
        user.LastExternalLoginUtc is null
            ? "Never"
            : $"{DateTime.SpecifyKind(user.LastExternalLoginUtc.Value, DateTimeKind.Utc):yyyy-MM-dd HH:mm} UTC";

    #endregion

    #region Navigation

    private Task ScrollToSmtpEmail() => ScrollToSection("smtp-email");
    private Task ScrollToUsers() => ScrollToSection("users");

    private async Task ScrollToSection(string sectionId)
    {
        await JS.InvokeVoidAsync("eval", $"document.getElementById('{sectionId}')?.scrollIntoView({{ behavior: 'smooth', block: 'start' }})");
    }

    #endregion
}
