using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClintonFrankland.Models;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Server-wide email settings used to send automated notices.
/// 
/// Note: SMTP credentials (host/username/password) are intentionally NOT stored here.
/// Those should be supplied via environment variables / server configuration.
/// </summary>
[Table("cfSmtpSettings")]
public class SmtpSetting : IModificationTracked
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; }

    [Column("SenderEmail")]
    [StringLength(256)]
    public string? SenderEmail { get; set; }

    [Column("FromName")]
    [StringLength(128)]
    public string? FromName { get; set; }

    [Column("TlsMode")]
    public SmtpTlsMode TlsMode { get; set; } = SmtpTlsMode.StartTls;

    [Column("AllowInvalidCerts")]
    public bool AllowInvalidCerts { get; set; }

    [Column("TestRecipientEmail")]
    [StringLength(256)]
    public string? TestRecipientEmail { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
