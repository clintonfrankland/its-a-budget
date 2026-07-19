using ClintonFrankland.Services;

namespace ClintonFrankland.Models.ViewModels;

public sealed record SpendPlanRow(string CategoryName, decimal Planned, decimal Actual)
{
    public decimal Variance => CurrencyPolicy.Round(Planned - Actual);
    public string Status => Planned <= 0m && Actual > 0m
        ? "Unbudgeted"
        : Variance < 0m
            ? "Over budget"
            : Variance > 0m
                ? "Under budget"
                : "On budget";
}

public sealed record MonthCloseVarianceSnapshot(
    DateOnly MonthStart,
    IReadOnlyList<SpendPlanRow> Rows,
    decimal TotalBudgeted,
    decimal TotalActual,
    decimal TotalVariance,
    IReadOnlyList<SpendPlanRow> NeedsAttention,
    IReadOnlyList<SpendPlanRow> LargestUnderBudget)
{
    public static MonthCloseVarianceSnapshot Create(DateOnly selectedMonth, IEnumerable<SpendPlanRow> sourceRows)
    {
        var rows = sourceRows
            .OrderBy(row => row.Variance < 0m ? 0 : row.Variance > 0m ? 1 : 2)
            .ThenBy(row => row.Variance < 0m ? row.Variance : 0m)
            .ThenByDescending(row => row.Variance > 0m ? row.Variance : 0m)
            .ThenBy(row => row.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var totalBudgeted = CurrencyPolicy.Round(rows.Sum(row => row.Planned));
        var totalActual = CurrencyPolicy.Round(rows.Sum(row => row.Actual));

        return new MonthCloseVarianceSnapshot(
            new DateOnly(selectedMonth.Year, selectedMonth.Month, 1),
            rows,
            totalBudgeted,
            totalActual,
            CurrencyPolicy.Round(totalBudgeted - totalActual),
            rows.Where(row => row.Variance < 0m).Take(3).ToList(),
            rows.Where(row => row.Variance > 0m)
                .OrderByDescending(row => row.Variance)
                .ThenBy(row => row.CategoryName, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList());
    }
}

public sealed record CashflowPoint(DateOnly Date, string Description, decimal Change, decimal Balance, bool IsStartingBalance = false);

public sealed record CashflowReport(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal StartingBalance,
    IReadOnlyList<CashflowPoint> Points,
    decimal LowestBalance,
    DateOnly LowestDate);

public sealed record NetWorthPoint(DateOnly Date, decimal Value);

public sealed record NetWorthReport(decimal CurrentTotal, DateOnly CurrentAsOfDate, IReadOnlyList<NetWorthPoint> History);
