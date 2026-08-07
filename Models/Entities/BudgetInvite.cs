using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfBudgetInvites")]
public class BudgetInvite : IModificationTracked
{
    [Key]
    [Column("BudgetInviteId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BudgetInviteId { get; set; }

    [Required]
    [Column("SharedBudgetId")]
    public int SharedBudgetId { get; set; }

    [Required]
    [Column("InvitedByUserId")]
    public int InvitedByUserId { get; set; }

    [Required]
    [Column("InviteTokenHash")]
    [StringLength(128)]
    public string InviteTokenHash { get; set; } = string.Empty;

    [Required]
    [Column("Role")]
    [StringLength(16)]
    public BudgetMemberRole Role { get; set; } = BudgetMemberRole.Viewer;

    [Column("InviteeEmail")]
    [StringLength(256)]
    public string? InviteeEmail { get; set; }

    [Column("InviteeUserName")]
    [StringLength(64)]
    public string? InviteeUserName { get; set; }

    [Required]
    [Column("ExpiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [Required]
    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("AcceptedAtUtc")]
    public DateTime? AcceptedAtUtc { get; set; }

    [Column("AcceptedByUserId")]
    public int? AcceptedByUserId { get; set; }

    [Column("RevokedAtUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [Column("RevokedByUserId")]
    public int? RevokedByUserId { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }

    [ForeignKey("InvitedByUserId")]
    public virtual User? InvitedByUser { get; set; }
}
