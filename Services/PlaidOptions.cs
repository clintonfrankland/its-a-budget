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
    /// <summary>Secret used to calculate the HMAC-SHA256 body signature for webhook intake.</summary>
    public string? WebhookSigningSecret { get; init; }

    public bool IsUsable => Enabled && Environment.Equals("sandbox", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
