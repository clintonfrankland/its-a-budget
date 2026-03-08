using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Audit/log record of an automated notification attempt.
/// Used for idempotency (avoid duplicate sends) and for troubleshooting.
/// </summary>
[Table("cfNotificationSendLog")]
public class NotificationSendLog
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("UserId")]
    public int UserId { get; set; }

    [Column("BudgetId")]
    public int? BudgetId { get; set; }

    /// <summary>
    /// The user's local date the notice applies to (used for "at most one per day" idempotency).
    /// Stored as a DateTime at midnight UTC to stay EF-friendly across providers.
    /// </summary>
    [Column("NoticeLocalDate")]
    public DateTime NoticeLocalDate { get; set; }

    [Column("NoticeType")]
    [StringLength(32)]
    public string NoticeType { get; set; } = string.Empty; // DueSoon|DueToday|PastDue

    [Column("Recipient")]
    [StringLength(256)]
    public string Recipient { get; set; } = string.Empty;

    [Column("Subject")]
    [StringLength(256)]
    public string Subject { get; set; } = string.Empty;

    [Column("Status")]
    [StringLength(16)]
    public string Status { get; set; } = string.Empty; // Sent|Failed

    [Column("ErrorMessage")]
    [StringLength(1024)]
    public string? ErrorMessage { get; set; }
}
