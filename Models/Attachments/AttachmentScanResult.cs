namespace ClintonFrankland.Models.Attachments;

public sealed record AttachmentScanResult(bool Accepted, string? Message)
{
    public static AttachmentScanResult Clean() => new(true, null);

    public static AttachmentScanResult Rejected(string message) => new(false, message);
}
