using System.Text.RegularExpressions;

namespace ClintonFrankland.Blazor.Tests;

public class GhcrWorkflowContractTests
{
    [Fact]
    public void PublishWorkflow_PinsActionsAndVerifiesThePublishedDigest()
    {
        var workflow = ReadRepoFile(".github/workflows/publish-ghcr.yml");
        var actionReferences = Regex.Matches(
            workflow,
            @"(?m)^\s*uses:\s+[^@\s]+@(?<reference>[^\s#]+)");

        Assert.NotEmpty(actionReferences);
        Assert.All(actionReferences.Cast<Match>(), actionReference =>
            Assert.Matches("^[0-9a-f]{40}$", actionReference.Groups["reference"].Value));
        Assert.Contains("id: build", workflow, StringComparison.Ordinal);
        Assert.Contains("EXPECTED_DIGEST: ${{ steps.build.outputs.digest }}", workflow, StringComparison.Ordinal);
        Assert.Contains("publishedVersion.name !== expectedDigest", workflow, StringComparison.Ordinal);
        Assert.Matches(@"(?s)core\.setFailed\(\s*`GHCR tags", workflow);
    }

    [Fact]
    public void PublishWorkflow_OnlyAcceptsSuccessfulMainPushTestsRuns()
    {
        var workflow = ReadRepoFile(".github/workflows/publish-ghcr.yml");

        Assert.Contains("github.event.workflow_run.event == 'push'", workflow, StringComparison.Ordinal);
        Assert.Contains("github.event.workflow_run.head_branch == 'main'", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("github.event.workflow_run.event == 'workflow_dispatch'", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DescribesWorkflowEnforcedImmutabilityAndResidualRace()
    {
        var readme = ReadRepoFile("README.md");

        Assert.Contains("workflow-enforced", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an atomic registry guarantee", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alternate writers", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("post-push", readme, StringComparison.OrdinalIgnoreCase);
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
