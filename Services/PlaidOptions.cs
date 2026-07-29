namespace ClintonFrankland.Services;

public sealed class PlaidOptions
{
    private static readonly Uri SandboxApiBaseAddress = new("https://sandbox.plaid.com/");
    private static readonly Uri ProductionApiBaseAddress = new("https://production.plaid.com/");

    public const string SectionName = "Plaid";
    public bool Enabled { get; init; }
    public string Environment { get; init; } = "sandbox";
    public string ClientName { get; init; } = "It's a Budget";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] Products { get; init; } = ["auth"];
    public string? RedirectUri { get; init; }

    public bool IsUsable => Enabled && ApiBaseAddress is not null
        && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public Uri? ApiBaseAddress => Environment.Trim().ToLowerInvariant() switch
    {
        "sandbox" => SandboxApiBaseAddress,
        "production" => ProductionApiBaseAddress,
        _ => null
    };
}
