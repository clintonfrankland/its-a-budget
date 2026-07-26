namespace ClintonFrankland.Blazor.Tests;

public sealed class PlaidConnectionUiSecurityTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void MutationEndpointsRequireAntiforgeryValidation()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "Program.cs"));

        var plaidEndpoints = source[source.IndexOf("/api/plaid/link-token", StringComparison.Ordinal)..source.IndexOf("app.MapRazorComponents", StringComparison.Ordinal)];
        Assert.DoesNotContain(".DisableAntiforgery()", plaidEndpoints, StringComparison.Ordinal);
        Assert.Contains("app.UseAntiforgery();", source, StringComparison.Ordinal);
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
