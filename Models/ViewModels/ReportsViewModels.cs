using ClintonFrankland.Services;

namespace ClintonFrankland.Models.ViewModels;

public enum MonthlyReportLineKind { Income, Bill, Allowance, Transfer }

public sealed record MonthlyReportLine(string Name, decimal Planned, decimal Actual, MonthlyReportLineKind Kind)
{
    // A single signed convention makes comparisons unambiguous: actual less planned is
    // unfavorable for income, while actual greater planned is unfavorable for expenses.
    public decimal Variance => CurrencyPolicy.Round(Actual - Planned);
    public string Indicator => Kind == MonthlyReportLineKind.Income
        ? Variance > 0m ? "Favorable" : Variance < 0m ? "Unfavorable" : "On plan"
        : Variance < 0m ? "Favorable" : Variance > 0m ? "Unfavorable" : "On plan";
}

public sealed record MonthlyReport(
    DateOnly MonthStart,
    IReadOnlyList<MonthlyReportLine> Income,
    IReadOnlyList<MonthlyReportLine> Bills,
    IReadOnlyList<MonthlyReportLine> Allowances,
    IReadOnlyList<MonthlyReportLine> Transfers,
    decimal PlannedNetCashFlow,
    decimal ActualNetCashFlow)
{
    public bool HasContent => Income.Count > 0 || Bills.Count > 0 || Allowances.Count > 0 || Transfers.Count > 0;
    public decimal NetCashFlowVariance => CurrencyPolicy.Round(ActualNetCashFlow - PlannedNetCashFlow);
    public string NetCashFlowIndicator => NetCashFlowVariance > 0m ? "Ahead of plan" : NetCashFlowVariance < 0m ? "Behind plan" : "On plan";
}

public sealed record SpendPlanRow(string CategoryName, decimal Planned, decimal Actual)
{
    public decimal Variance => CurrencyPolicy.Round(Planned - Actual);
    public string Status => Planned <= 0m && Actual > 0m ? "Unbudgeted" : Variance < 0m ? "Over budget" : Variance > 0m ? "Under budget" : "On budget";
}

public sealed record MonthCloseVarianceSnapshot(DateOnly MonthStart, IReadOnlyList<SpendPlanRow> Rows, decimal TotalBudgeted, decimal TotalActual, decimal TotalVariance, IReadOnlyList<SpendPlanRow> NeedsAttention, IReadOnlyList<SpendPlanRow> LargestUnderBudget)
{
    public static MonthCloseVarianceSnapshot Create(DateOnly selectedMonth, IEnumerable<SpendPlanRow> sourceRows)
    {
        var rows = sourceRows.OrderBy(row => row.Variance < 0m ? 0 : row.Variance > 0m ? 1 : 2).ThenBy(row => row.Variance < 0m ? row.Variance : 0m).ThenByDescending(row => row.Variance > 0m ? row.Variance : 0m).ThenBy(row => row.CategoryName, StringComparer.OrdinalIgnoreCase).ToList();
        return new(new DateOnly(selectedMonth.Year, selectedMonth.Month, 1), rows,
            CurrencyPolicy.Round(rows.Sum(row => row.Planned)), CurrencyPolicy.Round(rows.Sum(row => row.Actual)),
            CurrencyPolicy.Round(rows.Sum(row => row.Planned - row.Actual)), rows.Where(row => row.Variance < 0m).Take(3).ToList(), rows.Where(row => row.Variance > 0m).Take(3).ToList());
    }
}

public sealed record CashflowPoint(DateOnly Date, string Description, decimal Change, decimal Balance, bool IsStartingBalance = false);
public sealed record CashflowReport(DateOnly StartDate, DateOnly EndDate, decimal StartingBalance, IReadOnlyList<CashflowPoint> Points, decimal LowestBalance, DateOnly LowestDate);
public sealed record NetWorthPoint(DateOnly Date, decimal Value);
public sealed record NetWorthReport(decimal CurrentTotal, DateOnly CurrentAsOfDate, IReadOnlyList<NetWorthPoint> History);
