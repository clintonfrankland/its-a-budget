using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("cfTransactionRules")]
public class TransactionRule
{
    [Key] public int TransactionRuleId { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(256)] public string ContainsText { get; set; } = string.Empty;
    /// <summary>Stable Plaid merchant entity when available; takes precedence over description matching.</summary>
    [MaxLength(128)] public string? MerchantEntityId { get; set; }
    [MaxLength(256)] public string? NormalizedMerchant { get; set; }
    public int? AccountId { get; set; }
    [Column(TypeName = "decimal(9,2)")] public decimal? MinimumAmount { get; set; }
    [Column(TypeName = "decimal(9,2)")] public decimal? MaximumAmount { get; set; }
    [MaxLength(128)] public string? CategoryName { get; set; }
    [MaxLength(255)] public string? PayeeName { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    [MaxLength(32)] public string Source { get; set; } = TransactionRuleSource.Manual;
    [MaxLength(32)] public string ApprovalState { get; set; } = TransactionRuleApprovalState.Approved;
    public int MatchCount { get; set; }
    [Column(TypeName = "decimal(5,4)")] public decimal? Confidence { get; set; }
    [MaxLength(1000)] public string? EvidenceSummary { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class TransactionRuleSource { public const string Manual = "manual"; public const string Learned = "learned"; }
public static class TransactionRuleApprovalState { public const string Proposed = "proposed"; public const string Approved = "approved"; public const string Rejected = "rejected"; }
