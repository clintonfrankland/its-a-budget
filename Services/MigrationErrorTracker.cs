using System.Text.RegularExpressions;
using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed class MigrationErrorTracker
{
    private static readonly SemaphoreSlim TableEnsureLock = new(1, 1);

    private readonly ClintonFranklandDbContext _db;
    private readonly ILogger<MigrationErrorTracker> _logger;

    public MigrationErrorTracker(ClintonFranklandDbContext db, ILogger<MigrationErrorTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CaptureMigrationErrorAsync(Exception exception, string? migrationName = null, CancellationToken ct = default)
    {
        try
        {
            await EnsureTableExistsAsync(ct);

            var error = new MigrationError
            {
                MigrationName = string.IsNullOrWhiteSpace(migrationName) ? null : migrationName,
                ErrorMessage = SanitizeSensitiveData(exception.Message),
                StackTrace = SanitizeSensitiveData(exception.ToString()),
                OccurredAt = DateTime.UtcNow
            };

            _db.MigrationErrors.Add(error);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception trackerEx)
        {
            _logger.LogWarning(trackerEx, "Failed to persist migration error");
        }
    }

    public Task<MigrationError?> GetLatestErrorAsync(CancellationToken ct = default)
        => _db.MigrationErrors.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<MigrationError>> GetErrorHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await _db.MigrationErrors.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(safeLimit)
            .ToListAsync(ct);
    }

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await TableEnsureLock.WaitAsync(ct);
        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[MigrationErrors]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MigrationErrors]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [MigrationName] NVARCHAR(256) NULL,
        [ErrorMessage] NVARCHAR(MAX) NOT NULL,
        [StackTrace] NVARCHAR(MAX) NOT NULL,
        [OccurredAt] DATETIME2 NOT NULL CONSTRAINT [DF_MigrationErrors_OccurredAt] DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX [IX_MigrationErrors_OccurredAt] ON [dbo].[MigrationErrors]([OccurredAt]);
END
", ct);
        }
        finally
        {
            TableEnsureLock.Release();
        }
    }

    private static string SanitizeSensitiveData(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input;

        // Local/UNC file paths
        sanitized = Regex.Replace(sanitized, @"([A-Za-z]:\\[^\s\r\n;]+|\\\\[^\s\r\n;]+)", "[REDACTED_PATH]");

        // IPv4 addresses
        sanitized = Regex.Replace(sanitized, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[REDACTED_IP]");

        // Common secret-bearing key/value fragments
        sanitized = Regex.Replace(sanitized, @"(?i)(password|pwd|user id|uid|token|apikey|api_key|secret)\s*=\s*[^;\s]+", "$1=[REDACTED]");

        // Connection string fragments
        sanitized = Regex.Replace(sanitized, @"(?i)(server|data source|initial catalog|database)\s*=\s*[^;\r\n]+", "$1=[REDACTED]");

        return sanitized;
    }
}
