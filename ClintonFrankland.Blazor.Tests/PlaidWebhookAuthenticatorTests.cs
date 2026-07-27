using System.Security.Cryptography;
using System.Text;
using ClintonFrankland.Services;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidWebhookAuthenticatorTests
{
    [Fact]
    public void SignedBody_IsAccepted_AndTamperingIsRejected()
    {
        const string secret = "test-signing-secret";
        const string body = "{\"webhook_code\":\"SYNC_UPDATES_AVAILABLE\"}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        var authenticator = new PlaidWebhookAuthenticator(Options.Create(new PlaidOptions { WebhookSigningSecret = secret }));

        Assert.True(authenticator.IsValid(body, "sha256=" + signature));
        Assert.False(authenticator.IsValid(body + " ", signature));
        Assert.False(authenticator.IsValid(body, null));
    }
}
