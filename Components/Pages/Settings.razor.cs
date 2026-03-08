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
        await LoadSmtpSettingsAsync();
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

    #region User Management

    private async Task LoadUsersAsync()
    {
        var current = AuthService.CurrentUser;

        if (current.IsAdmin)
        {
            UsersList = await DbContext.Users.AsNoTracking().Where(u => !u.IsDeleted).OrderBy(u => u.UserId).ToListAsync();
        }
        else
        {
            UsersList = await DbContext.Users.AsNoTracking().Where(u => !u.IsDeleted && u.UserId == current.UserId).ToListAsync();
        }
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
        dbUser.Salt = PasswordUtility.CreateSalt();
        dbUser.PasswordHash = PasswordUtility.HashPassword(PasswordValue, dbUser.Salt);
        dbUser.PasswordResetRequestOn = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();
        ClosePasswordModal();
        UserErrorMessage = null;
        Message = $"Password reset for {PasswordUser.UserName}.";
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
