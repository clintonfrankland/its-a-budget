using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a budget item (recurring income or expense)
/// </summary>
[Table("cfBudgets")]
public class Budget
{
    [Key]
    [Column("BudgetId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BudgetId { get; set; }

    [Required]
    [Column("BudgetTypeID")]
    public int BudgetTypeId { get; set; }

    [Column("BudgetName")]
    [StringLength(255)]
    public string? BudgetName { get; set; }

    [Column("FrequencyID")]
    public int? FrequencyId { get; set; }

    [Column("NextDueDate")]
    public DateTime? NextDueDate { get; set; }

    [Column("EndDate")]
    public DateTime? EndDate { get; set; }

    [Column("Amount", TypeName = "decimal(18, 2)")]
    public decimal? Amount { get; set; }

    [Required]
    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Column("UserId")]
    public int? UserId { get; set; }

    [Column("SharedBudgetId")]
    public int? SharedBudgetId { get; set; }

    [Column("IsAutomatic")]
    public bool? IsAutomatic { get; set; }

    [Column("IsLate")]
    public bool? IsLate { get; set; }

    [Column("IsBill")]
    public bool? IsBill { get; set; }

    [Column("PayeeId")]
    public int? PayeeId { get; set; }

    // Navigation properties
    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }

    [ForeignKey("FrequencyId")]
    public virtual Frequency? Frequency { get; set; }

    [ForeignKey("PayeeId")]
    public virtual Payee? Payee { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }
}
