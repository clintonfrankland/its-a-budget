using System.Security.Cryptography;
using System.Text;

namespace ClintonFrankland.Services;

/// <summary>Validates a signed webhook body before it can enter the durable queue.</summary>
public sealed class PlaidWebhookAuthenticator(IPlaidClient plaidClient, TimeProvider timeProvider)
{
    /// <summary>Validates Plaid's ES256 Plaid-Verification JWT against the raw body.</summary>
    public async Task<PlaidWebhookVerification?> VerifyAsync(string requestBody, string? signedJwt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signedJwt)) return null;
        var parts = signedJwt.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var header = System.Text.Json.JsonDocument.Parse(Base64UrlDecode(parts[0])).RootElement;
            if (!header.TryGetProperty("alg", out var alg) || alg.GetString() != "ES256" ||
                !header.TryGetProperty("kid", out var kid) || string.IsNullOrWhiteSpace(kid.GetString())) return null;
            var key = await plaidClient.GetWebhookVerificationKeyAsync(kid.GetString()!, cancellationToken);
            if (key.KeyId != kid.GetString() || key.Algorithm != "ES256" || key.KeyType != "EC" || key.Curve != "P-256") return null;
            using var ecdsa = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64UrlDecode(key.X), Y = Base64UrlDecode(key.Y) } });
            if (!ecdsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) return null;
            var payload = System.Text.Json.JsonDocument.Parse(Base64UrlDecode(parts[1])).RootElement;
            if (!payload.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out var issuedAt) ||
                !payload.TryGetProperty("request_body_sha256", out var bodyHash) || string.IsNullOrWhiteSpace(bodyHash.GetString())) return null;
            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            if (issuedAt > now + 60 || now - issuedAt > 300) return null;
            var expectedHash = Convert.FromHexString(bodyHash.GetString()!);
            var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(requestBody));
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash)) return null;
            return new PlaidWebhookVerification(kid.GetString()!, issuedAt, bodyHash.GetString()!);
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

public sealed record PlaidWebhookVerification(string KeyId, long IssuedAtUnixSeconds, string RequestBodySha256);
