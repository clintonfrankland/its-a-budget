namespace ClintonFrankland.Models.Attachments;

public sealed record ReceiptAttachmentCleanupResult(
    int Scanned,
    int Deleted,
    int Retained,
    int Failed,
    int Skipped);
