namespace ClintonFrankland.Services;

public sealed class PlaidOptions
{
    public const string SectionName = "Plaid";
    public bool Enabled { get; init; }
    public string Environment { get; init; } = "sandbox";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] Products { get; init; } = ["auth"];
    public string? RedirectUri { get; init; }

    public bool IsUsable => Enabled && Environment.Equals("sandbox", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
