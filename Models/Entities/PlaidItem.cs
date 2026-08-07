using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>Encrypted server-side credentials for one Plaid Item owned by a Budget user.</summary>
[Table("cfPlaidItems")]
public class PlaidItem : IModificationTracked
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PlaidItemId { get; set; }

    public int UserId { get; set; }

    [Required, StringLength(128)]
    public string ItemId { get; set; } = string.Empty;

    // This is always ASP.NET Data Protection ciphertext, never a plaintext Plaid token.
    [Required]
    public string EncryptedAccessToken { get; set; } = string.Empty;

    [StringLength(128)]
    public string? InstitutionId { get; set; }

    [StringLength(256)]
    public string? InstitutionName { get; set; }

    [Required, StringLength(32)]
    public string Status { get; set; } = PlaidItemStatus.Active;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DisconnectedAtUtc { get; set; }
    public string? TransactionsCursor { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
    public virtual ICollection<PlaidAccountMapping> AccountMappings { get; set; } = [];
}

public static class PlaidItemStatus
{
    public const string Active = "active";
    public const string Disconnected = "disconnected";
}
