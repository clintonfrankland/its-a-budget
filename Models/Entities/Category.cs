using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a transaction category
/// </summary>
[Table("cfCategories")]
public class Category : IModificationTracked
{
    [Key]
    [Column("CategoryId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryId { get; set; }

    [Column("CategoryName")]
    [StringLength(255)]
    public string? CategoryName { get; set; }

    [Column("UserId")]
    public int? UserId { get; set; }

    [Column("SharedBudgetId")]
    public int? SharedBudgetId { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }

    public virtual ICollection<Budget> Budgets { get; set; } = [];
    public virtual ICollection<CategoryBudgetTarget> CategoryBudgetTargets { get; set; } = [];
    public virtual ICollection<Transaction> Transactions { get; set; } = [];
}
