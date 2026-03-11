namespace ClintonFrankland.Services;

public sealed class DatabaseBackupWorker : BackgroundService
{
    private readonly DatabaseBackupService _backup;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupWorker> _logger;

    public DatabaseBackupWorker(DatabaseBackupService backup, IConfiguration configuration, ILogger<DatabaseBackupWorker> logger)
    {
        _backup = backup;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("Backup:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("DatabaseBackupWorker disabled (Backup:Enabled=false)");
            return;
        }

        var outputDirectory = _configuration.GetValue<string>("Backup:OutputDirectory") ?? "/backups";
        var intervalHours = _configuration.GetValue<int?>("Backup:IntervalHours") ?? 24;
        var keepDays = _configuration.GetValue<int?>("Backup:KeepDays") ?? 14;

        if (intervalHours <= 0)
            intervalHours = 24;
        if (keepDays < 1)
            keepDays = 7;

        Directory.CreateDirectory(outputDirectory);

        _logger.LogInformation("DatabaseBackupWorker enabled. IntervalHours={IntervalHours} OutputDirectory={OutputDirectory} KeepDays={KeepDays}",
            intervalHours, outputDirectory, keepDays);

        // Run one immediately on startup, then on interval.
        await RunOnceAsync(outputDirectory, keepDays, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(outputDirectory, keepDays, stoppingToken);
        }
    }

    private async Task RunOnceAsync(string outputDirectory, int keepDays, CancellationToken ct)
    {
        try
        {
            await _backup.BackupDatabaseAsync(connectionString: null, outputDirectory, ct);
            CleanupOldBackups(outputDirectory, keepDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled database backup failed");
        }
    }

    private void CleanupOldBackups(string outputDirectory, int keepDays)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-keepDays);
            var files = Directory.EnumerateFiles(outputDirectory, "*.bak", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTimeUtc < cutoff)
                {
                    _logger.LogInformation("Deleting old backup file {File}", fi.FullName);
                    fi.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup cleanup failed");
        }
    }
}
