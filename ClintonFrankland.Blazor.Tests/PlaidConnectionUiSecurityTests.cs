using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidConnectionUiSecurityTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void MutationEndpointsExplicitlyRequireAntiforgeryValidation()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "Program.cs"));

        var plaidEndpoints = source[source.IndexOf("/api/plaid/link-token", StringComparison.Ordinal)..source.IndexOf("app.MapRazorComponents", StringComparison.Ordinal)];
        Assert.DoesNotContain(".DisableAntiforgery()", plaidEndpoints, StringComparison.Ordinal);
        Assert.Contains("app.UseAntiforgery();", source, StringComparison.Ordinal);
        Assert.Equal(5, plaidEndpoints.Split(".RequireAntiforgery()", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task ForgedMutationRequest_IsRejectedBeforeTheHandlerRuns()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAntiforgery();

        await using var app = builder.Build();
        var handlerRan = false;
        app.UseRouting();
        app.UseAntiforgery();
        app.MapPost("/plaid-mutation", () =>
        {
            handlerRan = true;
            return Results.NoContent();
        }).RequireAntiforgery();

        await app.StartAsync();
        var response = await app.GetTestClient().PostAsync("/plaid-mutation", content: null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(handlerRan);
    }

    [Fact]
    public void ConnectionPageLaunchesLinkAndRendersAccountMappingControls()
    {
        var markup = File.ReadAllText(Path.Combine(RepositoryRoot, "Components/Pages/PlaidConnections.razor"));
        var code = File.ReadAllText(Path.Combine(RepositoryRoot, "Components/Pages/PlaidConnections.razor.cs"));

        Assert.Contains("StartConnectionAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Map discovered accounts", markup, StringComparison.Ordinal);
        Assert.Contains("plaidLink.open", code, StringComparison.Ordinal);
        Assert.Contains("MapAccountAsync", code, StringComparison.Ordinal);
    }
}
