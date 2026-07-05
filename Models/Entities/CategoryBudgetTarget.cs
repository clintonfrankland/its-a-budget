using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a user's planned spending amount for one category in one month.
/// </summary>
[Table("cfCategoryBudgetTargets")]
public class CategoryBudgetTarget
{
    [Key]
    [Column("CategoryBudgetTargetId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryBudgetTargetId { get; set; }

    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    [Column("SharedBudgetId")]
    public int? SharedBudgetId { get; set; }

    [Required]
    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Required]
    [Column("BudgetMonth", TypeName = "date")]
    public DateOnly BudgetMonth { get; set; }

    [Required]
    [Column("PlannedAmount", TypeName = "decimal(18, 2)")]
    public decimal PlannedAmount { get; set; }

    [Required]
    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }
}
