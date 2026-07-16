namespace ClintonFrankland.Models;

public sealed record BudgetAllowanceProgress(
    int BudgetId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal PlannedAmount,
    decimal SpentAmount,
    decimal RemainingAmount);
