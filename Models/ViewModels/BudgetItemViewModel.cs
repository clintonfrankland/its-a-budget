namespace ClintonFrankland.Models;

public class BudgetItemViewModel
{
    public int BudgetId { get; set; }
    public DateTime DueDate { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Payee { get; set; } = string.Empty;  // Optional payee override
    public string Category { get; set; } = string.Empty;
    public string FrequencyName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public bool IsBill { get; set; }
    public bool IsAuto { get; set; }
    public bool IsLate { get; set; }
    public bool IsSpendingAllowance { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal PercentUsed => PlannedAmount > 0m
        ? Math.Round(SpentAmount / PlannedAmount * 100m, 2, MidpointRounding.AwayFromZero)
        : 0m;
    public string BehaviorName => IsSpendingAllowance ? "Allowance" : "Scheduled";
    public bool CanManageFinancialData { get; set; }
    public bool IsIncome => Amount >= 0;
    public bool HasCategoryWarning { get; set; }
    public string CategoryWarning { get; set; } = string.Empty;
    public bool CanRecordToCheckbook => CanManageFinancialData && !IsSpendingAllowance && !HasCategoryWarning;
    public string OccurrenceKey => $"{BudgetId}:{DueDate:yyyyMMdd}";

    // Additional fields for BudgetItems page
    public DateTime? EndDate { get; set; }
    public string EndDateName { get; set; } = string.Empty;
    public decimal Monthly { get; set; }
}
