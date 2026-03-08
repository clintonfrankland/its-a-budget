using ClintonFrankland.Data;
using ClintonFrankland.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace ClintonFrankland.Services;

public class EmailSenderService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly IConfiguration _configuration;

    public EmailSenderService(ClintonFranklandDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var smtpUi = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        if (smtpUi is null || !smtpUi.IsEnabled)
            throw new InvalidOperationException("SMTP is disabled.");

        if (string.IsNullOrWhiteSpace(smtpUi.SenderEmail))
            throw new InvalidOperationException("Sender Email is required.");

        var server = GetServerConfig();
        if (string.IsNullOrWhiteSpace(server.Host))
            throw new InvalidOperationException("SMTP Host is not configured on the server.");

        var port = server.Port <= 0 ? 587 : server.Port;

        var secureSocketOptions = smtpUi.TlsMode switch
        {
            SmtpTlsMode.None => SecureSocketOptions.None,
            SmtpTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls
        };

        var message = new MimeMessage();
        message.From.Add(string.IsNullOrWhiteSpace(smtpUi.FromName)
            ? MailboxAddress.Parse(smtpUi.SenderEmail)
            : new MailboxAddress(smtpUi.FromName, smtpUi.SenderEmail));

        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        if (smtpUi.AllowInvalidCerts)
        {
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        await client.ConnectAsync(server.Host, port, secureSocketOptions);

        // Authenticate only if a username was provided.
        if (!string.IsNullOrWhiteSpace(server.UserName))
        {
            await client.AuthenticateAsync(server.UserName, server.Password ?? "");
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public SmtpServerConfig GetServerConfig()
    {
        // Prefer env vars, but allow appsettings.json/appsettings.{ENV}.json too.
        // Suggested env var names:
        // - Email__Smtp__Host
        // - Email__Smtp__Port
        // - Email__Smtp__UserName
        // - Email__Smtp__Password
        var host = _configuration["Email:Smtp:Host"];
        var port = _configuration.GetValue<int?>("Email:Smtp:Port") ?? 587;
        var user = _configuration["Email:Smtp:UserName"];
        var pass = _configuration["Email:Smtp:Password"];

        return new SmtpServerConfig(host, port, user, pass);
    }
}
