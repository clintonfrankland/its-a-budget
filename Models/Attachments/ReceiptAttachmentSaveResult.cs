namespace ClintonFrankland.Models.Attachments;

public sealed record ReceiptAttachmentSaveResult(bool Succeeded, string? RelativePath, string? ErrorMessage)
{
    public static ReceiptAttachmentSaveResult Success(string relativePath) => new(true, relativePath, null);

    public static ReceiptAttachmentSaveResult Failure(string message) => new(false, null, message);
}
