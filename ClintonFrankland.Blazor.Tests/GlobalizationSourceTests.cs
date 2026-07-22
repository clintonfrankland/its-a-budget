using System.Globalization;

namespace ClintonFrankland.Blazor.Tests;

public class GlobalizationSourceTests
{
    [Fact]
    public void ProgramSource_PinsThreadAndRequestLocalizationCultureToEnUs()
    {
        var source = ReadRepoFile("Program.cs");

        Assert.Contains("CultureInfo.GetCultureInfo(\"en-US\")", source);
        Assert.Contains("CultureInfo.DefaultThreadCurrentCulture = usCulture;", source);
        Assert.Contains("CultureInfo.DefaultThreadCurrentUICulture = usCulture;", source);
        Assert.Contains("CultureInfo.CurrentCulture = usCulture;", source);
        Assert.Contains("CultureInfo.CurrentUICulture = usCulture;", source);
        Assert.Contains("builder.Services.Configure<RequestLocalizationOptions>", source);
        Assert.Contains(".SetDefaultCulture(usCulture.Name)", source);
        Assert.Contains(".AddSupportedCultures(usCulture.Name)", source);
        Assert.Contains(".AddSupportedUICultures(usCulture.Name)", source);
        Assert.Contains("app.UseRequestLocalization();", source);
    }

    [Fact]
    public void CurrencyDisplay_UsesConfiguredEnUsCurrencyFormat()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var amount = 1234.5m;

        var formatted = amount.ToString("C", culture);

        Assert.Contains("$", formatted);
        Assert.Equal("$1,234.50", formatted);
    }

    [Fact]
    public void Documentation_DescribesCulturePolicyAndReadmeLinksIt()
    {
        var globalization = ReadRepoFile("GLOBALIZATION.md");
        var readme = ReadRepoFile("README.md");

        Assert.Contains("development", globalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review/dispatch", globalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("production", globalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not configurable", globalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$", globalization);
        Assert.Contains("period for decimals", globalization);
        Assert.Contains("commas for grouped numbers", globalization);
        Assert.Contains("[GLOBALIZATION.md](GLOBALIZATION.md)", readme);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(path))
                return File.ReadAllText(path);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
