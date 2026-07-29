# Backups (SQL Server)

It's a Budget includes a simple SQL Server `.bak` export and a restore smoke test command.

## One-shot backup

From the repo folder:

```bash
dotnet run -- backup --out ./backups
```

This runs `BACKUP DATABASE ... WITH COPY_ONLY` to a timestamped `.bak` file.

## Restore smoke test

Proves a backup is actually restorable (not just created). Restores into a temporary database on the same SQL Server, runs a minimal query, then drops the temporary database.

```bash
dotnet run -- restore-smoketest --file ./backups/<yourfile>.bak
```

Notes:
- This requires SQL Server permissions to `RESTORE DATABASE` and `DROP DATABASE`.
- Some SQL Server instances require `WITH MOVE` (explicit file locations). If your instance errors with missing logical file mappings, we can enhance the restore command to query `RESTORE FILELISTONLY` and apply `MOVE`.

## Optional scheduled backups

The app can run scheduled backups as a hosted worker.

Add to `appsettings.json` (or environment variables):

```json
{
  "Backup": {
    "Enabled": false,
    "OutputDirectory": "/backups",
    "IntervalHours": 24,
    "KeepDays": 14
  }
}
```

`Enabled` defaults to `false`.
