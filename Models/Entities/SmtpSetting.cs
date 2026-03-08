using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfSmtpSettings")]
public class SmtpSetting
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; }

    [Column("Host")]
    [StringLength(256)]
    public string? Host { get; set; }

    [Column("Port")]
    public int Port { get; set; } = 587;

    [Column("UserName")]
    [StringLength(256)]
    public string? UserName { get; set; }

    [Column("Password")]
    [StringLength(512)]
    public string? Password { get; set; }

    [Column("SenderEmail")]
    [StringLength(256)]
    public string? SenderEmail { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
