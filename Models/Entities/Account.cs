using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a financial account (checking, credit card, etc.)
/// </summary>
[Table("cfAccounts")]
public class Account
{
    [Key]
    [Column("AccountId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AccountId { get; set; }

    [Required]
    [Column("AccountName")]
    [StringLength(255)]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    [Column("BeginningBalance", TypeName = "decimal(18, 2)")]
    public decimal BeginningBalance { get; set; }

    [Required]
    [Column("Balance", TypeName = "decimal(18, 2)")]
    public decimal Balance { get; set; }

    [Required]
    [Column("ClearedBalance", TypeName = "decimal(18, 2)")]
    public decimal ClearedBalance { get; set; }

    [Required]
    [Column("AccountTypeID")]
    public int AccountTypeId { get; set; }

    [Required]
    [Column("IsDefault")]
    public bool IsDefault { get; set; }

    [Column("UserId")]
    public int? UserId { get; set; }

    [Column("SharedBudgetId")]
    public int? SharedBudgetId { get; set; }

    [Column("AccountNumber")]
    [StringLength(16)]
    public string? AccountNumber { get; set; }

    [Column("DueDate")]
    public int? DueDate { get; set; }

    [Column("MinimumPayment", TypeName = "decimal(18, 2)")]
    public decimal? MinimumPayment { get; set; }

    [Column("InterestRate", TypeName = "decimal(18, 2)")]
    public decimal? InterestRate { get; set; }

    [Column("WebURL")]
    [StringLength(4000)]
    public string? WebUrl { get; set; }

    [Column("CreditLimit", TypeName = "decimal(18, 2)")]
    public decimal? CreditLimit { get; set; }

    [Column("AvailableCredit", TypeName = "decimal(18, 2)")]
    public decimal? AvailableCredit { get; set; }

    [Column("LastUpdated")]
    public DateTime? LastUpdated { get; set; }

    [Column("IsDeleted")]
    public bool? IsDeleted { get; set; }

    // Navigation properties
    [ForeignKey("AccountTypeId")]
    public virtual AccountType? AccountType { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("SharedBudgetId")]
    public virtual SharedBudget? SharedBudget { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = [];
}
