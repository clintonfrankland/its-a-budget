namespace ClintonFrankland.Models;

public class DashboardSnapshotViewModel
{
    public DateTime AsOfDate { get; set; }

    public decimal TodayBalance { get; set; }

    public decimal UpcomingBillsTotal { get; set; }

    /// <summary>
    /// The lowest projected balance over the next 6 months
    /// </summary>
    public decimal LowestProjectedBalance { get; set; }

    /// <summary>
    /// The date when the lowest projected balance occurs
    /// </summary>
    public DateTime LowestProjectedBalanceDate { get; set; }

    public List<DashboardBillItemViewModel> UpcomingBills { get; set; } = [];

    public List<DashboardCategorySpendViewModel> CategorySpend { get; set; } = [];
}

public class DashboardCategorySpendViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    /// <summary>Monthly-normalised budget for this category. Null if no budget item exists.</summary>
    public decimal? BudgetedMonthly { get; set; }
    public bool IsOnBudget => BudgetedMonthly.HasValue && Math.Abs(Total - BudgetedMonthly.Value) <= 1m;
}

public class DashboardBillItemViewModel
{
    public int BudgetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Payee { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPastDue { get; set; }
}
