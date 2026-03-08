namespace ClintonFrankland.Models;

public record SmtpServerConfig(
    string? Host,
    int Port,
    string? UserName,
    string? Password
);
