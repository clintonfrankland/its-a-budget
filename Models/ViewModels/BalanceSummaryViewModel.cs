namespace ClintonFrankland.Models;

public sealed record BalanceSummaryViewModel(
    decimal Balance,
    decimal ClearedBalance,
    decimal SafeToSpend,
    DateTime SafeToSpendDate,
    decimal MonthlyPlan);
