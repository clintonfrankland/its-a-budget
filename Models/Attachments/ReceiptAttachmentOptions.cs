namespace ClintonFrankland.Models.Attachments;

public sealed class ReceiptAttachmentOptions
{
    public const string SectionName = "ReceiptAttachments";
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    public string UploadRootRelativePath { get; set; } = "uploads/receipts";

    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".bmp",
        ".tif",
        ".tiff"
    ];

    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff"
    ];
}
