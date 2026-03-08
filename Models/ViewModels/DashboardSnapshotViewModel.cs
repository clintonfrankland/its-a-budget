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
