using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Shared household container for one or more Budget users.
/// </summary>
[Table("cfSharedBudgets")]
public class SharedBudget
{
    [Key]
    [Column("SharedBudgetId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SharedBudgetId { get; set; }

    [Required]
    [Column("Name")]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("OwnerUserId")]
    public int OwnerUserId { get; set; }

    [Required]
    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey("OwnerUserId")]
    public virtual User? OwnerUser { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = [];
    public virtual ICollection<Budget> Budgets { get; set; } = [];
    public virtual ICollection<BudgetInvite> Invites { get; set; } = [];
    public virtual ICollection<BudgetMember> Members { get; set; } = [];
    public virtual ICollection<Category> Categories { get; set; } = [];
    public virtual ICollection<CategoryBudgetTarget> CategoryBudgetTargets { get; set; } = [];
    public virtual ICollection<Transaction> Transactions { get; set; } = [];
}
