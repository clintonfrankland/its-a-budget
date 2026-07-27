using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>Raw Plaid transaction evidence. It is deliberately not a checkbook ledger entry.</summary>
[Table("cfPlaidTransactionStaging")]
public class PlaidTransactionStaging
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int PlaidTransactionStagingId { get; set; }
    public int UserId { get; set; }
    public int PlaidItemId { get; set; }
    public int BudgetAccountId { get; set; }
    [Required, StringLength(128)] public string PlaidTransactionId { get; set; } = string.Empty;
    [StringLength(128)] public string? PendingTransactionId { get; set; }
    [StringLength(128)] public string PlaidAccountId { get; set; } = string.Empty;
    public decimal PlaidAmount { get; set; } // Plaid: outflow positive; Budget ledger expenses are negative.
    [StringLength(3)] public string CurrencyCode { get; set; } = "USD";
    public DateOnly TransactionDate { get; set; }
    public bool IsPending { get; set; }
    public bool IsRemoved { get; set; }
    [StringLength(256)] public string? MerchantName { get; set; }
    [StringLength(256)] public string? Name { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}

[Table("cfPlaidSyncRuns")]
public class PlaidSyncRun
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int PlaidSyncRunId { get; set; }
    public int PlaidItemId { get; set; }
    [StringLength(64)] public string Status { get; set; } = "running";
    public string? CursorBefore { get; set; }
    public string? CursorAfter { get; set; }
    public int AddedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int RemovedCount { get; set; }
    [StringLength(512)] public string? ErrorCode { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

[Table("cfPlaidWebhookDeliveries")]
public class PlaidWebhookDelivery
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int PlaidWebhookDeliveryId { get; set; }
    [Required, StringLength(128)] public string DeliveryKey { get; set; } = string.Empty;
    [StringLength(128)] public string? ItemId { get; set; }
    [StringLength(64)] public string? WebhookType { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? QueuedAtUtc { get; set; }
}
