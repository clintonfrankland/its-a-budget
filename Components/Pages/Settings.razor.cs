using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Components.Pages;

public partial class Settings
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ClintonFrankland.Data.ClintonFranklandDbContext DbContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private EmailSenderService EmailSenderService { get; set; } = default!;

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

    private async Task PersistAsync()
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
        Message = "Server email settings saved.";
        MessageCss = "alert-success";
    }

    private async Task OnEnabledChanged(ChangeEventArgs e)
    {
        IsEnabled = e.Value is bool b && b;
        await PersistAsync();
    }

    private async Task OnSenderChanged(ChangeEventArgs e)
    {
        SenderEmail = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task OnFromNameChanged(ChangeEventArgs e)
    {
        FromName = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task OnTlsModeChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (Enum.TryParse<SmtpTlsMode>(raw, ignoreCase: true, out var mode))
            TlsMode = mode;

        await PersistAsync();
    }

    private async Task OnAllowInvalidCertsChanged(ChangeEventArgs e)
    {
        AllowInvalidCerts = e.Value is bool b && b;
        await PersistAsync();
    }

    private async Task OnTestRecipientChanged(ChangeEventArgs e)
    {
        TestRecipientEmail = e.Value?.ToString();
        await PersistAsync();
    }

    private bool IsSendingTest { get; set; }
    private List<string> TestLogLines { get; set; } = new();

    private void ClearTestLog()
    {
        TestLogLines.Clear();
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
        ClearTestLog();
        Message = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            await AppendTestLogAsync("Starting SMTP test...");
            await PersistAsync();

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
}
