namespace ClintonFrankland.Models.ViewModels;

public enum CategoryBudgetAlertStatus
{
    None,
    Warning,
    Overspent
}

public sealed record CategoryBudgetThresholds(int WarningPercent)
{
    public const int DefaultWarningPercent = 80;

    public static CategoryBudgetThresholds Validate(int warningPercent)
    {
        if (warningPercent is < 1 or > 100)
            throw new InvalidOperationException("Category budget warning percentage must be between 1 and 100.");

        return new CategoryBudgetThresholds(warningPercent);
    }
}

public sealed record CategoryBudgetCategoryOption(
    int CategoryId,
    string CategoryName,
    bool CanManageFinancialData);

public sealed record CategoryBudgetMonthRow(
    int? TargetId,
    int CategoryId,
    string CategoryName,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal RemainingAmount,
    decimal PercentUsed,
    CategoryBudgetAlertStatus AlertStatus,
    bool CanManageFinancialData)
{
    public bool IsBudgeted => TargetId.HasValue;
}

public sealed record CategoryBudgetSaveRequest(
    int? TargetId,
    int CategoryId,
    DateOnly BudgetMonth,
    decimal PlannedAmount);
