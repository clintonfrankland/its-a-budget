namespace ClintonFrankland.Services;

public class StartupDiagnosticsState
{
    public bool Checked { get; set; }
    public bool HasIssue { get; set; }
    public string? FailureType { get; set; } // connectivity | migration
    public string? ErrorMessage { get; set; }
    public string? LastAppliedMigration { get; set; }
    public string[] SafeSteps { get; set; } =
    [
        "Verify the SQL Server connection string for this environment (review DB, not prod).",
        "Check database reachability and credentials from the running container.",
        "Inspect __EFMigrationsHistory to confirm the latest applied migration.",
        "Run migrations safely: dotnet ef database update against the intended review database.",
        "Confirm container build context matches the repo you edited before redeploying."
    ];
}
