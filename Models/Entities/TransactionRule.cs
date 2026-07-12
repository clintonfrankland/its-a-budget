using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfTransactionRules")]
public class TransactionRule
{
    [Key] public int TransactionRuleId { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(256)] public string ContainsText { get; set; } = string.Empty;
    public int? AccountId { get; set; }
    [Column(TypeName = "decimal(9,2)")] public decimal? MinimumAmount { get; set; }
    [Column(TypeName = "decimal(9,2)")] public decimal? MaximumAmount { get; set; }
    [MaxLength(128)] public string? CategoryName { get; set; }
    [MaxLength(255)] public string? PayeeName { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
}
