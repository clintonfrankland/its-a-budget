using ClintonFrankland.Data;
using ClintonFrankland.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace ClintonFrankland.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}

public class EmailSenderService : IEmailSender
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
        await SendWithDiagnosticsAsync(toEmail, subject, body, log: null);
    }

    public async Task SendWithDiagnosticsAsync(string toEmail, string subject, string body, Action<string>? log)
    {
        void Write(string msg)
        {
            log?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }

        Write("Loading SMTP settings...");
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

        Write($"Server: {server.Host}:{port} (TLS mode: {smtpUi.TlsMode})");
        Write($"Sender: {smtpUi.SenderEmail}{(string.IsNullOrWhiteSpace(smtpUi.FromName) ? "" : $" ({smtpUi.FromName})")}");
        Write($"Recipient: {toEmail}");

        Write("Building message...");
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
            Write("WARNING: AllowInvalidCerts is enabled. TLS certificate validation is disabled.");
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        Write("Connecting to SMTP server...");
        await client.ConnectAsync(server.Host, port, secureSocketOptions);
        Write("Connected.");

        if (!string.IsNullOrWhiteSpace(server.UserName))
        {
            Write("Authenticating...");
            await client.AuthenticateAsync(server.UserName, server.Password ?? "");
            Write("Authenticated.");
        }
        else
        {
            Write("No SMTP username configured. Skipping authentication.");
        }

        Write("Sending message...");
        await client.SendAsync(message);
        Write("Message accepted by server.");

        await client.DisconnectAsync(true);
        Write("Disconnected.");
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
