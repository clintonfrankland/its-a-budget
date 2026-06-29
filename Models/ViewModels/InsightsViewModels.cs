namespace ClintonFrankland.Models.ViewModels;

public sealed record InsightsMonth(DateOnly StartDate, string Label);

public sealed record CategorySpendingTotal(string CategoryName, decimal Total);

public sealed record CategoryTrendRow(string CategoryName, IReadOnlyList<decimal> MonthlyTotals)
{
    public decimal Total => MonthlyTotals.Sum();
}

public sealed record TopPayeeSpending(string PayeeName, decimal Total, int TransactionCount);
