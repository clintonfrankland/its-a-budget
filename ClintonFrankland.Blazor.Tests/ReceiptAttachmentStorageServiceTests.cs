using ClintonFrankland.Data;
using ClintonFrankland.Models.Attachments;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClintonFrankland.Blazor.Tests;

public sealed class ReceiptAttachmentStorageServiceTests
{
    [Fact]
    public void CheckbookSource_UsesAttachmentServiceForUploadReplacementRemovalAndTransactionDelete()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Components", "Pages", "Checkbook.razor.cs"));

        Assert.Contains("ReceiptAttachmentStorage.SaveAsync(editAttachmentFile, userId)", source);
        Assert.Contains("ReceiptAttachmentStorage.DeleteIfManagedAsync(previousAttachmentPath)", source);
        Assert.Contains("ReceiptAttachmentStorage.DeleteIfManagedAsync(attachmentPath)", source);
        Assert.DoesNotContain("SaveAttachmentFileAsync", source);
        Assert.DoesNotContain("Path.Combine(Environment.WebRootPath, relativePath)", source);
    }

    [Fact]
    public async Task SaveAsync_AcceptsPdfWithGeneratedRelativePathAndInvokesScanner()
    {
        await using var fixture = new AttachmentFixture();
        var scanner = new RecordingScanner(AttachmentScanResult.Clean());
        var service = fixture.CreateService(scanner);
        var file = new TestBrowserFile("receipt.pdf", "application/pdf", [1, 2, 3]);

        var result = await service.SaveAsync(file, userId: 42);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.RelativePath);
        Assert.Matches(@"^uploads/receipts/42/[a-f0-9]{32}\.pdf$", result.RelativePath);
        Assert.True(File.Exists(fixture.WebRootPath(result.RelativePath!)));
        Assert.Equal(1, scanner.ScanCount);
    }

    [Fact]
    public async Task SaveAsync_RejectsLargeOrUnsupportedFilesBeforeSaving()
    {
        await using var fixture = new AttachmentFixture(new ReceiptAttachmentOptions { MaxFileSizeBytes = 3 });
        var scanner = new RecordingScanner(AttachmentScanResult.Clean());
        var service = fixture.CreateService(scanner);

        var largeResult = await service.SaveAsync(new TestBrowserFile("receipt.pdf", "application/pdf", [1, 2, 3, 4]), 42);
        var unsupportedResult = await service.SaveAsync(new TestBrowserFile("receipt.exe", "application/octet-stream", [1]), 42);

        Assert.False(largeResult.Succeeded);
        Assert.Contains("3 bytes", largeResult.ErrorMessage);
        Assert.False(unsupportedResult.Succeeded);
        Assert.Contains("Unsupported", unsupportedResult.ErrorMessage);
        Assert.Equal(0, scanner.ScanCount);
        Assert.False(Directory.Exists(Path.Combine(fixture.WebRootPath(), "uploads", "receipts", "42")));
    }

    [Fact]
    public async Task SaveAsync_RemovesPartialFileWhenScannerRejects()
    {
        await using var fixture = new AttachmentFixture();
        var service = fixture.CreateService(new RecordingScanner(AttachmentScanResult.Rejected("Blocked by scanner.")));

        var result = await service.SaveAsync(new TestBrowserFile("receipt.png", "image/png", [1, 2, 3]), 42);

        Assert.False(result.Succeeded);
        Assert.Equal("Blocked by scanner.", result.ErrorMessage);
        var userDirectory = Path.Combine(fixture.WebRootPath(), "uploads", "receipts", "42");
        Assert.True(Directory.Exists(userDirectory));
        Assert.Empty(Directory.EnumerateFiles(userDirectory));
    }

    [Fact]
    public async Task SaveAsync_RemovesPartialFileWhenScannerFails()
    {
        await using var fixture = new AttachmentFixture();
        var service = fixture.CreateService(new ThrowingScanner());

        var result = await service.SaveAsync(new TestBrowserFile("receipt.png", "image/png", [1, 2, 3]), 42);

        Assert.False(result.Succeeded);
        Assert.Contains("could not be read, scanned, or saved", result.ErrorMessage);
        var userDirectory = Path.Combine(fixture.WebRootPath(), "uploads", "receipts", "42");
        Assert.Empty(Directory.EnumerateFiles(userDirectory));
    }

    [Fact]
    public async Task DeleteIfManagedAsync_DeletesOnlyFilesUnderReceiptRoot()
    {
        await using var fixture = new AttachmentFixture();
        var service = fixture.CreateService();
        var managedPath = "uploads/receipts/42/receipt.pdf";
        var managedFullPath = fixture.WebRootPath(managedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(managedFullPath)!);
        await File.WriteAllTextAsync(managedFullPath, "receipt");

        var outsidePath = fixture.WebRootPath("uploads/other.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(outsidePath)!);
        await File.WriteAllTextAsync(outsidePath, "outside");

        Assert.True(await service.DeleteIfManagedAsync(managedPath));
        Assert.False(await service.DeleteIfManagedAsync("../other.pdf"));
        Assert.False(await service.DeleteIfManagedAsync("uploads/receipts/42/../../other.pdf"));
        Assert.False(File.Exists(managedFullPath));
        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task CleanupOrphansAsync_DeletesUnreferencedManagedFilesAndRetainsReferencedFiles()
    {
        await using var fixture = new AttachmentFixture();
        fixture.Db.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            UserId = 42,
            TransactionDate = new DateOnly(2026, 6, 29),
            Amount = -12.34m,
            PayeeId = 1,
            CategoryId = 1,
            AccountId = 1,
            Cleared = false,
            AttachmentPath = "uploads/receipts/42/keep.pdf"
        });
        fixture.Db.Transactions.Add(new Transaction
        {
            TransactionId = 2,
            UserId = 42,
            TransactionDate = new DateOnly(2026, 6, 29),
            Amount = -56.78m,
            PayeeId = 1,
            CategoryId = 1,
            AccountId = 1,
            Cleared = false,
            AttachmentPath = "../bad.pdf"
        });
        await fixture.Db.SaveChangesAsync();

        var keepPath = fixture.WebRootPath("uploads/receipts/42/keep.pdf");
        var deletePath = fixture.WebRootPath("uploads/receipts/42/delete.pdf");
        var tempPath = fixture.WebRootPath("uploads/receipts/42/pending.uploading");
        Directory.CreateDirectory(Path.GetDirectoryName(keepPath)!);
        await File.WriteAllTextAsync(keepPath, "keep");
        await File.WriteAllTextAsync(deletePath, "delete");
        await File.WriteAllTextAsync(tempPath, "pending");

        var result = await fixture.CreateService().CleanupOrphansAsync();

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Deleted);
        Assert.Equal(1, result.Retained);
        Assert.Equal(2, result.Skipped);
        Assert.True(File.Exists(keepPath));
        Assert.False(File.Exists(deletePath));
        Assert.True(File.Exists(tempPath));
    }

    private sealed class AttachmentFixture : IAsyncDisposable
    {
        private readonly string _rootPath;
        private readonly ReceiptAttachmentOptions _options;

        public AttachmentFixture(ReceiptAttachmentOptions? options = null)
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"budget-attachments-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootPath);
            _options = options ?? new ReceiptAttachmentOptions();
            Db = CreateDbContext();
        }

        public ClintonFranklandDbContext Db { get; }

        public string WebRootPath(string? relativePath = null)
            => relativePath is null ? _rootPath : Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public ReceiptAttachmentStorageService CreateService(IAttachmentMalwareScanner? scanner = null)
            => new(
                Db,
                new TestWebHostEnvironment(_rootPath),
                scanner ?? new RecordingScanner(AttachmentScanResult.Clean()),
                Options.Create(_options),
                NullLogger<ReceiptAttachmentStorageService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }

        private static ClintonFranklandDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ClintonFranklandDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ClintonFranklandDbContext(options);
        }
    }

    private sealed class RecordingScanner : IAttachmentMalwareScanner
    {
        private readonly AttachmentScanResult _result;

        public RecordingScanner(AttachmentScanResult result) => _result = result;

        public int ScanCount { get; private set; }

        public Task<AttachmentScanResult> ScanAsync(string filePath, string originalFileName, string contentType, CancellationToken cancellationToken = default)
        {
            ScanCount++;
            Assert.True(File.Exists(filePath));
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingScanner : IAttachmentMalwareScanner
    {
        public Task<AttachmentScanResult> ScanAsync(string filePath, string originalFileName, string contentType, CancellationToken cancellationToken = default)
            => throw new ApplicationException("Scanner unavailable.");
    }

    private sealed class TestBrowserFile : IBrowserFile
    {
        private readonly byte[] _content;

        public TestBrowserFile(string name, string contentType, byte[] content)
        {
            Name = name;
            ContentType = contentType;
            _content = content;
            Size = content.Length;
        }

        public string Name { get; }
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
        public long Size { get; }
        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
                throw new IOException("File is larger than the maximum allowed size.");

            return new MemoryStream(_content);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            WebRootFileProvider = new PhysicalFileProvider(webRootPath);
            ContentRootPath = webRootPath;
            ContentRootFileProvider = WebRootFileProvider;
        }

        public string ApplicationName { get; set; } = "ClintonFrankland.Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }
}
