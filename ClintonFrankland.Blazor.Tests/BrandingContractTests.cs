namespace ClintonFrankland.Blazor.Tests;

public class BrandingContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void PublicBrandingUsesItsABudgetAndCurrentAssets()
    {
        var app = File.ReadAllText(Path.Combine(ProjectRoot, "Components", "App.razor"));
        var home = File.ReadAllText(Path.Combine(ProjectRoot, "Components", "Pages", "Home.razor"));
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Components", "Layout", "MainLayout.razor"));
        var settings = File.ReadAllText(Path.Combine(ProjectRoot, "appsettings.json"));

        Assert.Contains("It's a Budget", app, StringComparison.Ordinal);
        Assert.Contains("It's a Budget", home, StringComparison.Ordinal);
        Assert.Contains("its-a-budget-social.png", home, StringComparison.Ordinal);
        Assert.Contains("its-a-budget-48.png", layout, StringComparison.Ordinal);
        Assert.Contains("\"SiteName\": \"It's a Budget\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("SproutPenny", home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FooterRendersConfiguredVersionAsAnExplicitRazorExpression()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectRoot, "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("v@(SiteInfoService.SiteInfo.Version)", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("v@SiteInfoService.SiteInfo.Version", layout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("About.razor", "/about")]
    [InlineData("Privacy.razor", "/privacy")]
    [InlineData("Terms.razor", "/terms")]
    [InlineData("Security.razor", "/security")]
    [InlineData("DataDeletion.razor", "/data-deletion")]
    [InlineData("Contact.razor", "/contact")]
    public void PublicTrustPageHasExpectedRouteAndMetadata(string fileName, string route)
    {
        var page = File.ReadAllText(Path.Combine(ProjectRoot, "Components", "Pages", fileName));

        Assert.Contains($"@page \"{route}\"", page, StringComparison.Ordinal);
        Assert.Contains("<PageTitle>", page, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("budget.clintandtara.com", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiredBrandAssetsExist()
    {
        var assetDirectory = Path.Combine(ProjectRoot, "wwwroot", "images", "brand");
        var requiredAssets = new[]
        {
            "its-a-budget-logo.png",
            "its-a-budget-logo-web.png",
            "its-a-budget-mark.png",
            "its-a-budget-horizontal.png",
            "its-a-budget-social.png",
            "its-a-budget-16.png",
            "its-a-budget-32.png",
            "its-a-budget-48.png",
            "its-a-budget-180.png",
            "its-a-budget-192.png",
            "its-a-budget-512.png"
        };

        foreach (var asset in requiredAssets)
            Assert.True(File.Exists(Path.Combine(assetDirectory, asset)), $"Missing brand asset: {asset}");
    }
}
