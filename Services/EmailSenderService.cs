using System.Net;
using System.Net.Mail;
using ClintonFrankland.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public class EmailSenderService
{
    private readonly ClintonFranklandDbContext _db;

    public EmailSenderService(ClintonFranklandDbContext db)
    {
        _db = db;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var smtp = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        if (smtp is null || !smtp.IsEnabled)
            throw new InvalidOperationException("SMTP is disabled.");

        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.SenderEmail))
            throw new InvalidOperationException("SMTP host/sender are required.");

        using var client = new SmtpClient(smtp.Host, smtp.Port <= 0 ? 587 : smtp.Port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(smtp.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtp.UserName, smtp.Password ?? "")
        };

        using var message = new MailMessage(smtp.SenderEmail, toEmail, subject, body)
        {
            IsBodyHtml = false
        };

        await client.SendMailAsync(message);
    }
}
