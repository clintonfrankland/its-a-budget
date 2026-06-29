using ClintonFrankland.Data;
using ClintonFrankland.Models.Attachments;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Services;

public sealed class ReceiptAttachmentStorageService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IAttachmentMalwareScanner _scanner;
    private readonly ReceiptAttachmentOptions _options;
    private readonly ILogger<ReceiptAttachmentStorageService> _logger;
    private readonly HashSet<string> _allowedExtensions;
    private readonly HashSet<string> _allowedContentTypes;

    public ReceiptAttachmentStorageService(
        ClintonFranklandDbContext db,
        IWebHostEnvironment environment,
        IAttachmentMalwareScanner scanner,
        IOptions<ReceiptAttachmentOptions> options,
        ILogger<ReceiptAttachmentStorageService> logger)
    {
        _db = db;
        _environment = environment;
        _scanner = scanner;
        _options = options.Value;
        _logger = logger;
        _allowedExtensions = _options.AllowedExtensions
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allowedContentTypes = _options.AllowedContentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string AcceptedFileDescription => "PDF, JPG, PNG, GIF, WebP, BMP, or TIFF";

    public long MaxFileSizeBytes => _options.MaxFileSizeBytes > 0
        ? _options.MaxFileSizeBytes
        : ReceiptAttachmentOptions.DefaultMaxFileSizeBytes;

    public async Task<ReceiptAttachmentSaveResult> SaveAsync(IBrowserFile file, int userId, CancellationToken cancellationToken = default)
    {
        var validationMessage = Validate(file);
        if (validationMessage is not null)
            return ReceiptAttachmentSaveResult.Failure(validationMessage);

        var userDirectory = GetUserDirectory(userId);
        Directory.CreateDirectory(userDirectory);

        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var finalFileName = $"{Guid.NewGuid():N}{extension}";
        var finalPath = Path.Combine(userDirectory, finalFileName);
        var tempPath = Path.Combine(userDirectory, $"{Guid.NewGuid():N}.uploading");

        try
        {
            await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.OpenReadStream(MaxFileSizeBytes, cancellationToken).CopyToAsync(target, cancellationToken);
            }

            var scanResult = await _scanner.ScanAsync(tempPath, file.Name, file.ContentType, cancellationToken);
            if (!scanResult.Accepted)
            {
                DeleteFileIfExists(tempPath);
                return ReceiptAttachmentSaveResult.Failure(scanResult.Message ?? "The receipt was rejected by attachment scanning.");
            }

            File.Move(tempPath, finalPath);
            return ReceiptAttachmentSaveResult.Success(ToRelativePath(userId, finalFileName));
        }
        catch (IOException ex)
        {
            DeleteFileIfExists(tempPath);
            DeleteFileIfExists(finalPath);
            _logger.LogWarning(ex, "Receipt attachment upload failed for user {UserId}", userId);
            return ReceiptAttachmentSaveResult.Failure("The receipt could not be read or saved. Please try again.");
        }
        catch (InvalidOperationException ex)
        {
            DeleteFileIfExists(tempPath);
            DeleteFileIfExists(finalPath);
            _logger.LogWarning(ex, "Receipt attachment upload exceeded the configured size limit for user {UserId}", userId);
            return ReceiptAttachmentSaveResult.Failure($"Receipt files must be {FormatBytes(MaxFileSizeBytes)} or smaller.");
        }
    }

    public Task<bool> DeleteIfManagedAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.FromResult(false);

        if (!TryResolveManagedPath(relativePath, out var fullPath))
        {
            _logger.LogWarning("Skipped deleting unmanaged receipt attachment path {RelativePath}", relativePath);
            return Task.FromResult(false);
        }

        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        try
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed deleting receipt attachment {RelativePath}", relativePath);
            return Task.FromResult(false);
        }
    }

    public async Task<ReceiptAttachmentCleanupResult> CleanupOrphansAsync(CancellationToken cancellationToken = default)
    {
        var root = GetReceiptRootDirectory();
        if (!Directory.Exists(root))
        {
            _logger.LogInformation("Receipt attachment cleanup skipped because {Root} does not exist", root);
            return new ReceiptAttachmentCleanupResult(0, 0, 0, 0, 1);
        }

        var referencedPaths = await _db.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AttachmentPath != null && transaction.AttachmentPath != string.Empty)
            .Select(transaction => transaction.AttachmentPath!)
            .ToListAsync(cancellationToken);

        var skipped = 0;
        var managedReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var referencedPath in referencedPaths)
        {
            if (TryNormalizeManagedRelativePath(referencedPath, out var normalized))
            {
                managedReferences.Add(normalized!);
                continue;
            }

            skipped++;
            _logger.LogWarning("Skipped malformed receipt attachment reference {RelativePath} during cleanup", referencedPath);
        }

        var scanned = 0;
        var deleted = 0;
        var retained = 0;
        var failed = 0;

        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Path.GetFileName(filePath).EndsWith(".uploading", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (!TryGetManagedRelativePathFromFullPath(filePath, out var relativePath))
            {
                skipped++;
                continue;
            }

            scanned++;
            if (managedReferences.Contains(relativePath))
            {
                retained++;
                continue;
            }

            try
            {
                File.Delete(filePath);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
                _logger.LogWarning(ex, "Failed deleting orphaned receipt attachment {RelativePath}", relativePath);
            }
        }

        _logger.LogInformation(
            "Receipt attachment cleanup complete. Scanned={Scanned} Deleted={Deleted} Retained={Retained} Failed={Failed} Skipped={Skipped}",
            scanned,
            deleted,
            retained,
            failed,
            skipped);

        return new ReceiptAttachmentCleanupResult(scanned, deleted, retained, failed, skipped);
    }

    private string? Validate(IBrowserFile file)
    {
        if (file.Size <= 0)
            return "The selected receipt is empty or unreadable.";

        if (file.Size > MaxFileSizeBytes)
            return $"Receipt files must be {FormatBytes(MaxFileSizeBytes)} or smaller.";

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension) || !_allowedExtensions.Contains(extension))
            return $"Unsupported receipt file type. Use {AcceptedFileDescription}.";

        if (string.IsNullOrWhiteSpace(file.ContentType) || !_allowedContentTypes.Contains(file.ContentType))
            return $"Unsupported receipt content type. Use {AcceptedFileDescription}.";

        return null;
    }

    private string GetReceiptRootDirectory()
    {
        var rootRelativePath = NormalizeRelativePath(_options.UploadRootRelativePath);
        return Path.GetFullPath(Path.Combine(_environment.WebRootPath, rootRelativePath));
    }

    private string GetUserDirectory(int userId) => Path.Combine(GetReceiptRootDirectory(), userId.ToString());

    private string ToRelativePath(int userId, string fileName)
        => $"{NormalizeRelativePath(_options.UploadRootRelativePath)}/{userId}/{fileName}";

    private bool TryResolveManagedPath(string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (!TryNormalizeManagedRelativePath(relativePath, out _))
            return false;

        var candidate = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
        var root = GetReceiptRootDirectory();
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidate;
        return true;
    }

    private bool TryNormalizeManagedRelativePath(string relativePath, out string? normalized)
    {
        normalized = null;
        var normalizedRoot = NormalizeRelativePath(_options.UploadRootRelativePath);
        var candidate = NormalizeRelativePath(relativePath);

        if (Path.IsPathRooted(relativePath) ||
            candidate.Contains("..", StringComparison.Ordinal) ||
            !candidate.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rootSegments = normalizedRoot.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != rootSegments.Length + 2)
            return false;

        if (!int.TryParse(segments[^2], out _))
            return false;

        var extension = Path.GetExtension(segments[^1]);
        if (string.IsNullOrWhiteSpace(extension) || !_allowedExtensions.Contains(extension))
            return false;

        normalized = candidate;
        return true;
    }

    private bool TryGetManagedRelativePathFromFullPath(string fullPath, out string relativePath)
    {
        relativePath = string.Empty;
        var root = GetReceiptRootDirectory();
        var candidate = Path.GetFullPath(fullPath);
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        var localPath = Path.GetRelativePath(root, candidate).Replace(Path.DirectorySeparatorChar, '/');
        if (localPath.StartsWith("..", StringComparison.Ordinal))
            return false;

        relativePath = $"{NormalizeRelativePath(_options.UploadRootRelativePath)}/{localPath}";
        return TryNormalizeManagedRelativePath(relativePath, out _);
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim('/');

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes % (1024 * 1024) == 0)
            return $"{bytes / (1024 * 1024)} MB";

        return $"{bytes:N0} bytes";
    }
}
