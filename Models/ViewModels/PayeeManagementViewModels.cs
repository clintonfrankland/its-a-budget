namespace ClintonFrankland.Models.ViewModels;

public sealed class PayeeSummaryViewModel
{
    public int PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateOnly? LastTransactionDate { get; set; }
    public int BudgetCount { get; set; }
    public decimal Income30Days { get; set; }
    public decimal Expense30Days { get; set; }
    public decimal Income6Months { get; set; }
    public decimal Expense6Months { get; set; }
    public decimal Income1Year { get; set; }
    public decimal Expense1Year { get; set; }
    public string DuplicateHint { get; set; } = string.Empty;
}

public sealed record PayeeMergeSourcePreview(
    int PayeeId,
    string PayeeName,
    int TransactionCount,
    int BudgetCount);

public sealed record PayeeMergePreview(
    int KeepPayeeId,
    string KeepPayeeName,
    IReadOnlyList<PayeeMergeSourcePreview> RemovedPayees)
{
    public int TransactionCount => RemovedPayees.Sum(p => p.TransactionCount);
    public int BudgetCount => RemovedPayees.Sum(p => p.BudgetCount);
}

public sealed record PayeeMergeResult(
    int KeepPayeeId,
    IReadOnlyList<int> RemovedPayeeIds,
    int TransactionsUpdated,
    int BudgetsUpdated);
