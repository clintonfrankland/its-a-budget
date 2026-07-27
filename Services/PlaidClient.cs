using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Services;

public sealed record PlaidLinkToken(string Token, DateTimeOffset Expiration);
public sealed record PlaidExchangeResult(string AccessToken, string ItemId);
public sealed record PlaidDiscoveredAccount(string AccountId, string Name, string? Mask, string Type, string Subtype);
public sealed record PlaidSyncPage(IReadOnlyList<PlaidSyncTransaction> Added, IReadOnlyList<PlaidSyncTransaction> Modified, IReadOnlyList<string> Removed, string NextCursor, bool HasMore);
public sealed record PlaidSyncTransaction(string TransactionId, string AccountId, decimal Amount, string IsoCurrencyCode, DateOnly Date, bool Pending, string? PendingTransactionId, string? MerchantName, string? Name);
public sealed class PlaidSyncMutationDuringPaginationException : Exception { public PlaidSyncMutationDuringPaginationException() : base("Plaid transactions changed during pagination.") { } }

public interface IPlaidClient
{
    Task<PlaidLinkToken> CreateLinkTokenAsync(int userId, bool updateMode, string? accessToken, CancellationToken cancellationToken);
    Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken);
    Task<PlaidSyncPage> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken);
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
        // Plaid update-mode Link tokens identify the existing Item with its access token.
        // Products are intentionally omitted: specifying them can request an unintended
        // product update and is not required for ordinary credential-maintenance Link.
        var request = new LinkTokenRequest(_options.ClientId, _options.ClientSecret, $"budget-{userId}",
            updateMode ? null : _options.Products, "en", new[] { "US" }, updateMode ? accessToken : null, _options.RedirectUri);
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

    public async Task<PlaidSyncPage> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await PostAsync<SyncRequest, SyncResponse>("transactions/sync", new(_options.ClientId, _options.ClientSecret, accessToken, cursor), cancellationToken);
        return new(response.Added.Select(ToTransaction).ToList(), response.Modified.Select(ToTransaction).ToList(), response.Removed.Select(x => x.TransactionId).ToList(), response.NextCursor, response.HasMore);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsUsable)
            throw new InvalidOperationException("Plaid Sandbox is not configured. Configure Plaid:Enabled, ClientId, and ClientSecret on the server.");
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
        if (path == "transactions/sync" && !response.IsSuccessStatusCode)
        {
            var failure = await response.Content.ReadAsStringAsync(cancellationToken);
            if (failure.Contains("TRANSACTIONS_SYNC_MUTATION_DURING_PAGINATION", StringComparison.Ordinal))
                throw new PlaidSyncMutationDuringPaginationException();
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Plaid returned an empty response for {path}.");
    }

    private sealed class LinkTokenRequest
    {
        public LinkTokenRequest(string clientId, string secret, string clientUserId, string[]? products,
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
        [JsonPropertyName("products"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Products { get; }
        [JsonPropertyName("language")] public string Language { get; }
        [JsonPropertyName("country_codes")] public string[] CountryCodes { get; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; }
        [JsonPropertyName("redirect_uri")] public string? RedirectUri { get; }
    }
    private sealed record TokenExchangeRequest([property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret, [property: JsonPropertyName("public_token")] string PublicToken);
    private sealed record AccessTokenRequest([property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret, [property: JsonPropertyName("access_token")] string AccessToken);
    private sealed record SyncRequest([property: JsonPropertyName("client_id")] string ClientId, [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("cursor")] string? Cursor);
    private sealed record LinkTokenResponse([property: JsonPropertyName("link_token")] string LinkToken,
        [property: JsonPropertyName("expiration")] DateTimeOffset Expiration);
    private sealed record TokenExchangeResponse([property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("item_id")] string ItemId);
    private sealed record AccountsResponse([property: JsonPropertyName("accounts")] List<PlaidAccountResponse> Accounts);
    private sealed record PlaidAccountResponse([property: JsonPropertyName("account_id")] string AccountId,
        [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("mask")] string? Mask,
        [property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("subtype")] string Subtype);
    private sealed record SyncResponse([property: JsonPropertyName("added")] List<SyncTransactionResponse> Added, [property: JsonPropertyName("modified")] List<SyncTransactionResponse> Modified,
        [property: JsonPropertyName("removed")] List<RemovedTransactionResponse> Removed, [property: JsonPropertyName("next_cursor")] string NextCursor, [property: JsonPropertyName("has_more")] bool HasMore);
    private sealed record RemovedTransactionResponse([property: JsonPropertyName("transaction_id")] string TransactionId);
    private sealed record SyncTransactionResponse([property: JsonPropertyName("transaction_id")] string TransactionId, [property: JsonPropertyName("account_id")] string AccountId,
        [property: JsonPropertyName("amount")] decimal Amount, [property: JsonPropertyName("iso_currency_code")] string? IsoCurrencyCode, [property: JsonPropertyName("date")] DateOnly Date,
        [property: JsonPropertyName("pending")] bool Pending, [property: JsonPropertyName("pending_transaction_id")] string? PendingTransactionId,
        [property: JsonPropertyName("merchant_name")] string? MerchantName, [property: JsonPropertyName("name")] string? Name);
    private static PlaidSyncTransaction ToTransaction(SyncTransactionResponse source) => new(source.TransactionId, source.AccountId, source.Amount,
        source.IsoCurrencyCode ?? "USD", source.Date, source.Pending, source.PendingTransactionId, source.MerchantName, source.Name);
}
