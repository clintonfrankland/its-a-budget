using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfBudgetMembers")]
public class BudgetMember
{
    [Key]
    [Column("BudgetMemberId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BudgetMemberId { get; set; }

    [Required]
    [Column("SharedBudgetId")]
    public int SharedBudgetId { get; set; }

    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    [Required]
    [Column("Role")]
    [StringLength(16)]
    public BudgetMemberRole Role { get; set; } = BudgetMemberRole.Viewer;

    [Required]
    [Column("Status")]
    [StringLength(16)]
    public BudgetMemberStatus Status { get; set; } = BudgetMemberStatus.Active;

    [Required]
    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("RemovedAtUtc")]
    public DateTime? RemovedAtUtc { get; set; }

    [Column("RemovedByUserId")]
    public int? RemovedByUserId { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
