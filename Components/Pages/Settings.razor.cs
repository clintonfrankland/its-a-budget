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
    private string? Host { get; set; }
    private int Port { get; set; } = 587;
    private string? UserName { get; set; }
    private string? Password { get; set; }
    private string? SenderEmail { get; set; }

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
            smtp = new SmtpSetting { Id = 1, UpdatedAtUtc = DateTime.UtcNow, Port = 587 };
            DbContext.SmtpSettings.Add(smtp);
            await DbContext.SaveChangesAsync();
        }

        IsEnabled = smtp.IsEnabled;
        Host = smtp.Host;
        Port = smtp.Port <= 0 ? 587 : smtp.Port;
        UserName = smtp.UserName;
        Password = smtp.Password;
        SenderEmail = smtp.SenderEmail;
    }

    private async Task PersistAsync()
    {
        var smtp = await DbContext.SmtpSettings.FirstAsync(x => x.Id == 1);
        smtp.IsEnabled = IsEnabled;
        smtp.Host = string.IsNullOrWhiteSpace(Host) ? null : Host.Trim();
        smtp.Port = Port <= 0 ? 587 : Port;
        smtp.UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName.Trim();
        smtp.Password = string.IsNullOrWhiteSpace(Password) ? null : Password;
        smtp.SenderEmail = string.IsNullOrWhiteSpace(SenderEmail) ? null : SenderEmail.Trim();
        smtp.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync();
        Message = "SMTP settings saved.";
        MessageCss = "alert-success";
    }

    private async Task OnEnabledChanged(ChangeEventArgs e)
    {
        IsEnabled = e.Value is bool b && b;
        await PersistAsync();
    }

    private async Task OnHostChanged(ChangeEventArgs e)
    {
        Host = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task OnPortChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var p) && p > 0 && p <= 65535)
            Port = p;
        await PersistAsync();
    }

    private async Task OnUsernameChanged(ChangeEventArgs e)
    {
        UserName = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task OnPasswordChanged(ChangeEventArgs e)
    {
        Password = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task OnSenderChanged(ChangeEventArgs e)
    {
        SenderEmail = e.Value?.ToString();
        await PersistAsync();
    }

    private async Task SendTestAsync()
    {
        try
        {
            await PersistAsync();
            var target = AuthService.CurrentUser.EmailAddress ?? SenderEmail;
            if (string.IsNullOrWhiteSpace(target))
            {
                Message = "Set your profile email or sender email before sending a test.";
                MessageCss = "alert-warning";
                return;
            }

            await EmailSenderService.SendAsync(target, "Budget App SMTP Test", "SMTP configuration is working.");
            Message = $"Test email sent to {target}.";
            MessageCss = "alert-success";
        }
        catch (Exception ex)
        {
            Message = $"Failed to send test email: {ex.Message}";
            MessageCss = "alert-danger";
        }
    }
}
