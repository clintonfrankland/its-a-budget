using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Services;

/// <summary>Validates a signed webhook body before it can enter the durable queue.</summary>
public sealed class PlaidWebhookAuthenticator(IOptions<PlaidOptions> options)
{
    public bool IsValid(string requestBody, string? signature)
    {
        var secret = options.Value.WebhookSigningSecret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature)) return false;

        // The header is deliberately a body MAC, not a reusable shared-secret header.
        // Accept an optional conventional "sha256=" prefix for reverse-proxy support.
        var supplied = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? signature[7..] : signature;
        byte[] suppliedBytes;
        try { suppliedBytes = Convert.FromHexString(supplied); }
        catch (FormatException) { return false; }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(requestBody));
        return CryptographicOperations.FixedTimeEquals(expected, suppliedBytes);
    }
}
