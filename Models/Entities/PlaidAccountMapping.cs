using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>Explicit, case-sensitive association between a Plaid account and a Budget account.</summary>
[Table("cfPlaidAccountMappings")]
public class PlaidAccountMapping
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PlaidAccountMappingId { get; set; }

    public int PlaidItemId { get; set; }

    [Required, StringLength(128)]
    public string PlaidAccountId { get; set; } = string.Empty;

    public int BudgetAccountId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    [ForeignKey(nameof(PlaidItemId))]
    public virtual PlaidItem? PlaidItem { get; set; }
    [ForeignKey(nameof(BudgetAccountId))]
    public virtual Account? BudgetAccount { get; set; }
}
