using ClintonFrankland.Data;
using ClintonFrankland.Models;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

public sealed class BudgetAllowanceService
{
    private readonly ClintonFranklandDbContext _db;
    private readonly SharedBudgetDataService _sharedBudgets;

    public BudgetAllowanceService(ClintonFranklandDbContext db, SharedBudgetDataService? sharedBudgets = null)
    {
        _db = db;
        _sharedBudgets = sharedBudgets ?? new SharedBudgetDataService(db);
    }

    public async Task<Dictionary<int, BudgetAllowanceProgress>> GetCurrentProgressAsync(
        int userId,
        IEnumerable<Budget> budgets,
        DateOnly? asOf = null)
    {
        var day = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var allowances = budgets.Where(b => b.IsSpendingAllowance && IsActiveOn(b, day)).ToList();
        if (allowances.Count == 0)
            return [];

        var readable = await _sharedBudgets.GetReadableSharedBudgetIdsAsync(userId);
        var categoryIds = allowances.Select(b => b.CategoryId).Distinct().ToList();
        var earliest = allowances.Min(b => GetCurrentPeriod(b, day).Start);
        var transactions = await _db.ReadableTransactions(userId, readable)
            .Where(t => t.Amount < 0 && categoryIds.Contains(t.CategoryId) && t.TransactionDate >= earliest && t.TransactionDate <= day)
            .Select(t => new { t.CategoryId, t.TransactionDate, t.Amount, t.SharedBudgetId, t.UserId })
            .ToListAsync();

        return allowances.ToDictionary(b => b.BudgetId, b =>
        {
            var period = GetCurrentPeriod(b, day);
            var spent = CurrencyPolicy.Round(transactions
                .Where(t => t.CategoryId == b.CategoryId &&
                    t.TransactionDate >= period.Start && t.TransactionDate < period.End &&
                    SameBudgetScope(b, t.SharedBudgetId, t.UserId))
                .Sum(t => -t.Amount));
            var planned = CurrencyPolicy.Round(b.Amount ?? 0m);
            return new BudgetAllowanceProgress(
                b.BudgetId,
                period.Start,
                period.End,
                planned,
                spent,
                CurrencyPolicy.Round(planned - spent));
        });
    }

    public static (DateOnly Start, DateOnly End) GetCurrentPeriod(Budget budget, DateOnly asOf)
    {
        var frequencyId = budget.FrequencyId ?? 0;
        if (frequencyId == 0)
            throw new InvalidOperationException("A spending allowance must use a recurring frequency.");

        var end = NormalizeBoundary(
            DateOnly.FromDateTime((budget.NextDueDate ?? asOf.ToDateTime(TimeOnly.MinValue)).Date),
            frequencyId);
        while (end <= asOf)
        {
            var next = DateOnly.FromDateTime(BudgetScheduleService.CalculateNextDueDate(end.ToDateTime(TimeOnly.MinValue), frequencyId));
            if (next <= end)
                throw new InvalidOperationException("The spending allowance frequency does not advance.");
            end = next;
        }

        return (PreviousBoundary(end, frequencyId), end);
    }

    public static DateOnly GetEffectiveStart(Budget budget)
    {
        var frequencyId = budget.FrequencyId ?? 0;
        if (frequencyId == 0)
            throw new InvalidOperationException("A spending allowance must use a recurring frequency.");

        var firstBoundary = NormalizeBoundary(
            DateOnly.FromDateTime((budget.NextDueDate ?? DateTime.Today).Date),
            frequencyId);
        return PreviousBoundary(firstBoundary, frequencyId);
    }

    public static bool IsActiveOn(Budget budget, DateOnly day)
    {
        if (!budget.IsSpendingAllowance || day < GetEffectiveStart(budget))
            return false;

        return !HasEndDate(budget) || day <= DateOnly.FromDateTime(budget.EndDate!.Value);
    }

    public static bool Overlaps(Budget first, Budget second)
    {
        var firstStart = GetEffectiveStart(first);
        var secondStart = GetEffectiveStart(second);
        var firstEnd = HasEndDate(first) ? DateOnly.FromDateTime(first.EndDate!.Value) : DateOnly.MaxValue;
        var secondEnd = HasEndDate(second) ? DateOnly.FromDateTime(second.EndDate!.Value) : DateOnly.MaxValue;
        return firstStart <= secondEnd && secondStart <= firstEnd;
    }

    public static DateOnly PreviousBoundary(DateOnly boundary, int frequencyId) => frequencyId switch
    {
        1 => boundary.AddDays(-7),
        2 => boundary.AddDays(-14),
        4 => boundary.AddMonths(-1),
        5 => boundary.AddMonths(-2),
        6 => boundary.AddMonths(-3),
        7 => boundary.AddDays(-35),
        8 => boundary.Day == 15
            ? new DateOnly(boundary.Year, boundary.Month, 1)
            : new DateOnly(boundary.AddMonths(-1).Year, boundary.AddMonths(-1).Month, 15),
        9 => boundary.AddYears(-1),
        10 => boundary.AddDays(-5),
        11 => boundary.AddDays(-42),
        12 => boundary.AddDays(-21),
        13 => boundary.AddDays(-28),
        14 => boundary.AddMonths(-6),
        _ => throw new InvalidOperationException("The spending allowance frequency is not supported.")
    };

    private static DateOnly NormalizeBoundary(DateOnly boundary, int frequencyId)
    {
        if (frequencyId != 8 || boundary.Day is 1 or 15)
            return boundary;

        return boundary.Day < 15
            ? new DateOnly(boundary.Year, boundary.Month, 15)
            : new DateOnly(boundary.AddMonths(1).Year, boundary.AddMonths(1).Month, 1);
    }

    private static bool HasEndDate(Budget budget) =>
        budget.EndDate.HasValue && budget.EndDate.Value.Date != new DateTime(1970, 1, 1);

    private static bool SameBudgetScope(Budget budget, int? transactionSharedBudgetId, int? transactionUserId) =>
        budget.SharedBudgetId.HasValue
            ? transactionSharedBudgetId == budget.SharedBudgetId
            : !transactionSharedBudgetId.HasValue && transactionUserId == budget.UserId;
}
