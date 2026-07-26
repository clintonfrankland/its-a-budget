using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Services;

public sealed record PlaidLinkToken(string Token, DateTimeOffset Expiration);
public sealed record PlaidExchangeResult(string AccessToken, string ItemId);
public sealed record PlaidDiscoveredAccount(string AccountId, string Name, string? Mask, string Type, string Subtype);

public interface IPlaidClient
{
    Task<PlaidLinkToken> CreateLinkTokenAsync(int userId, bool updateMode, string? accessToken, CancellationToken cancellationToken);
    Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken);
    Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken);
}

/// <summary>Small server-only Plaid REST client. It deliberately never exposes client credentials in a DTO.</summary>
public sealed class PlaidClient : IPlaidClient
{
    private readonly HttpClient _httpClient;
    private readonly PlaidOptions _options;

    public PlaidClient(HttpClient httpClient, IOptions<PlaidOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PlaidLinkToken> CreateLinkTokenAsync(int userId, bool updateMode, string? accessToken, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var request = new LinkTokenRequest(_options.ClientId, _options.ClientSecret, $"budget-{userId}",
            _options.Products, "en", new[] { "US" }, updateMode ? accessToken : null, _options.RedirectUri);
        var response = await PostAsync<LinkTokenRequest, LinkTokenResponse>("link/token/create", request, cancellationToken);
        return new PlaidLinkToken(response.LinkToken, response.Expiration);
    }

    public async Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await PostAsync<TokenExchangeRequest, TokenExchangeResponse>("item/public_token/exchange",
            new(_options.ClientId, _options.ClientSecret, publicToken), cancellationToken);
        return new PlaidExchangeResult(response.AccessToken, response.ItemId);
    }

    public async Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await PostAsync<AccessTokenRequest, AccountsResponse>("accounts/get",
            new(_options.ClientId, _options.ClientSecret, accessToken), cancellationToken);
        return response.Accounts.Select(account => new PlaidDiscoveredAccount(
            account.AccountId, account.Name, account.Mask, account.Type, account.Subtype)).ToList();
    }

    public async Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        await PostAsync<AccessTokenRequest, object>("item/remove", new(_options.ClientId, _options.ClientSecret, accessToken), cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsUsable)
            throw new InvalidOperationException("Plaid Sandbox is not configured. Configure Plaid:Enabled, ClientId, and ClientSecret on the server.");
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Plaid returned an empty response for {path}.");
    }

    private sealed class LinkTokenRequest
    {
        public LinkTokenRequest(string clientId, string secret, string clientUserId, string[] products,
            string language, string[] countryCodes, string? accessToken, string? redirectUri)
        {
            ClientId = clientId;
            Secret = secret;
            User = new { client_user_id = clientUserId };
            Products = products;
            Language = language;
            CountryCodes = countryCodes;
            AccessToken = accessToken;
            RedirectUri = redirectUri;
        }

        [JsonPropertyName("client_id")] public string ClientId { get; }
        [JsonPropertyName("secret")] public string Secret { get; }
        [JsonPropertyName("user")] public object User { get; }
        [JsonPropertyName("products")] public string[] Products { get; }
        [JsonPropertyName("language")] public string Language { get; }
        [JsonPropertyName("country_codes")] public string[] CountryCodes { get; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; }
        [JsonPropertyName("redirect_uri")] public string? RedirectUri { get; }
    }
    private sealed record TokenExchangeRequest([property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret, [property: JsonPropertyName("public_token")] string PublicToken);
    private sealed record AccessTokenRequest([property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret, [property: JsonPropertyName("access_token")] string AccessToken);
    private sealed record LinkTokenResponse([property: JsonPropertyName("link_token")] string LinkToken,
        [property: JsonPropertyName("expiration")] DateTimeOffset Expiration);
    private sealed record TokenExchangeResponse([property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("item_id")] string ItemId);
    private sealed record AccountsResponse([property: JsonPropertyName("accounts")] List<PlaidAccountResponse> Accounts);
    private sealed record PlaidAccountResponse([property: JsonPropertyName("account_id")] string AccountId,
        [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("mask")] string? Mask,
        [property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("subtype")] string Subtype);
}
