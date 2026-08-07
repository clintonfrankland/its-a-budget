using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Server-wide settings for automated bill-due email notifications.
/// </summary>
[Table("cfBillDueNotificationSettings")]
public class BillDueNotificationSetting : IModificationTracked
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Send "due soon" notices when a bill is due within this many days.
    /// Example: 3 means due today + next 3 days.
    /// </summary>
    [Column("DueSoonDays")]
    public int DueSoonDays { get; set; } = 3;

    [Column("PastDueEnabled")]
    public bool PastDueEnabled { get; set; } = true;

    /// <summary>
    /// Safety limit for past-due notices. A bill more than this many days past due will not generate reminders.
    /// </summary>
    [Column("PastDueMaxDays")]
    public int PastDueMaxDays { get; set; } = 30;

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
