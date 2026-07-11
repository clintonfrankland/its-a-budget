using ClintonFrankland.Services;

namespace ClintonFrankland.Models.ViewModels;

public sealed record SpendPlanRow(string CategoryName, decimal Planned, decimal Actual)
{
    public decimal Variance => CurrencyPolicy.Round(Planned - Actual);
    public string Status => Variance < 0 ? "Over" : Variance > 0 ? "Under" : "On plan";
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
