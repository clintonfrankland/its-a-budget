using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed class DatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(IConfiguration configuration, ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GetDefaultConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    public static string GetDatabaseName(string connectionString)
    {
        var csb = new SqlConnectionStringBuilder(connectionString);

        // InitialCatalog is the canonical name for SqlClient.
        if (!string.IsNullOrWhiteSpace(csb.InitialCatalog))
            return csb.InitialCatalog;

        throw new InvalidOperationException("Connection string does not specify Initial Catalog (database name).");
    }

    public async Task<string> BackupDatabaseAsync(
        string? connectionString,
        string outputDirectory,
        CancellationToken ct)
    {
        connectionString ??= GetDefaultConnectionString();

        Directory.CreateDirectory(outputDirectory);

        var dbName = GetDatabaseName(connectionString);
        var fileName = $"{dbName}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var fullPath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));

        _logger.LogInformation("Starting SQL backup. Database={DbName} File={File}", dbName, fullPath);

        // BACKUP DATABASE cannot run inside a user transaction.
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // COPY_ONLY avoids breaking differential backup chains.
        // COMPRESSION may require SQL Server edition support; if it fails, the user can retry without.
        var sql = @"
BACKUP DATABASE [{0}] TO DISK = @path
WITH INIT, COPY_ONLY, COMPRESSION;
";

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 60 * 30; // 30 minutes
        cmd.CommandText = string.Format(sql, dbName.Replace("]", "]]"));
        cmd.Parameters.AddWithValue("@path", fullPath);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("SQL backup complete. File={File}", fullPath);
            return fullPath;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL backup failed. Database={DbName} File={File}", dbName, fullPath);
            throw;
        }
    }

    /// <summary>
    /// Restore a .bak into a temporary database on the same server, run a minimal query, then drop it.
    /// This is intended as a smoke test to prove backups are restorable.
    /// </summary>
    public async Task RestoreSmokeTestAsync(
        string? connectionString,
        string backupFile,
        CancellationToken ct)
    {
        connectionString ??= GetDefaultConnectionString();

        var sourceDbName = GetDatabaseName(connectionString);
        var restoreDbName = $"{sourceDbName}_restore_test_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

        _logger.LogInformation("Starting restore smoke test. SourceDb={SourceDb} RestoreDb={RestoreDb} BackupFile={BackupFile}",
            sourceDbName, restoreDbName, backupFile);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Switch to master for restore.
        await using (var useMaster = conn.CreateCommand())
        {
            useMaster.CommandText = "USE [master];";
            await useMaster.ExecuteNonQueryAsync(ct);
        }

        // We do not attempt MOVE of files; we assume SQL Server can place them (common on Windows installs).
        // If the instance requires explicit MOVE, SQL Server will error with a helpful message.
        var restoreSql = $@"
RESTORE DATABASE [{restoreDbName}] FROM DISK = @path WITH REPLACE;
";

        try
        {
            await using (var restoreCmd = conn.CreateCommand())
            {
                restoreCmd.CommandTimeout = 60 * 60; // 60 minutes
                restoreCmd.CommandText = restoreSql;
                restoreCmd.Parameters.AddWithValue("@path", Path.GetFullPath(backupFile));
                await restoreCmd.ExecuteNonQueryAsync(ct);
            }

            // Basic verification: ensure a few core tables exist and are queryable.
            await using (var verifyCmd = conn.CreateCommand())
            {
                verifyCmd.CommandTimeout = 60;
                verifyCmd.CommandText = $@"
SELECT
  (SELECT COUNT(1) FROM [{restoreDbName}].dbo.cfUsers) AS UsersCount,
  (SELECT COUNT(1) FROM [{restoreDbName}].dbo.cfAccounts) AS AccountsCount,
  (SELECT COUNT(1) FROM [{restoreDbName}].dbo.cfTransactions) AS TransactionsCount;
";

                await using var reader = await verifyCmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    var users = reader.GetInt32(0);
                    var accounts = reader.GetInt32(1);
                    var tx = reader.GetInt32(2);
                    _logger.LogInformation("Restore verification OK. Users={Users} Accounts={Accounts} Transactions={Transactions}", users, accounts, tx);
                }
            }
        }
        finally
        {
            // Drop the restored db even if verify fails.
            try
            {
                await using var dropCmd = conn.CreateCommand();
                dropCmd.CommandTimeout = 60 * 5;
                dropCmd.CommandText = $@"
IF DB_ID(@db) IS NOT NULL
BEGIN
  ALTER DATABASE [{restoreDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [{restoreDbName}];
END
";
                dropCmd.Parameters.AddWithValue("@db", restoreDbName);
                await dropCmd.ExecuteNonQueryAsync(ct);
                _logger.LogInformation("Restore smoke test cleanup complete. Dropped {RestoreDb}", restoreDbName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drop restore smoke test database {RestoreDb}", restoreDbName);
            }
        }
    }
}
