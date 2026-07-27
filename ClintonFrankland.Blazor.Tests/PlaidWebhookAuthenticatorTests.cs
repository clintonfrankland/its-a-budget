using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidWebhookAuthenticatorTests
{
    [Fact]
    public async Task PlaidVerificationJwt_IsAccepted_AndTamperingIsRejected()
    {
        const string body = "{\"webhook_code\":\"SYNC_UPDATES_AVAILABLE\"}";
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url("{\"alg\":\"ES256\",\"kid\":\"test-key\"}");
        var payload = Base64Url($"{{\"iat\":{now},\"request_body_sha256\":\"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant()}\"}}");
        var signature = Base64Url(key.SignData(System.Text.Encoding.ASCII.GetBytes($"{header}.{payload}"), System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var authenticator = new PlaidWebhookAuthenticator(new KeyClient(key.ExportParameters(false)), TimeProvider.System);

        Assert.NotNull(await authenticator.VerifyAsync(body, $"{header}.{payload}.{signature}", CancellationToken.None));
        Assert.Null(await authenticator.VerifyAsync(body + " ", $"{header}.{payload}.{signature}", CancellationToken.None));
        Assert.Null(await authenticator.VerifyAsync(body, null, CancellationToken.None));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Base64Url(string value) => Base64Url(System.Text.Encoding.UTF8.GetBytes(value));
    private sealed class KeyClient(System.Security.Cryptography.ECParameters parameters) : IPlaidClient
    {
        public Task<PlaidWebhookVerificationKey> GetWebhookVerificationKeyAsync(string keyId, CancellationToken ct) => Task.FromResult(new PlaidWebhookVerificationKey("test-key", "ES256", "EC", "P-256", Base64Url(parameters.Q.X!), Base64Url(parameters.Q.Y!)));
        public Task<PlaidLinkToken> CreateLinkTokenAsync(int u, bool a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidExchangeResult> ExchangePublicTokenAsync(string p, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaidDiscoveredAccount>> GetAccountsAsync(string a, CancellationToken c) => throw new NotSupportedException();
        public Task<PlaidSyncPage> SyncTransactionsAsync(string a, string? b, CancellationToken c) => throw new NotSupportedException();
        public Task RemoveItemAsync(string a, CancellationToken c) => throw new NotSupportedException();
    }
}
