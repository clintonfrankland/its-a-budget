using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfAuthLoginAudit")]
public class AuthLoginAudit
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("AttemptedAtUtc")]
    public DateTime AttemptedAtUtc { get; set; }

    [Column("UserName")]
    [StringLength(128)]
    public string? UserName { get; set; }

    [Column("ClientIp")]
    [StringLength(64)]
    public string? ClientIp { get; set; }

    [Required]
    [Column("Succeeded")]
    public bool Succeeded { get; set; }

    [Column("Reason")]
    [StringLength(256)]
    public string? Reason { get; set; }
}
