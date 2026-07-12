using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ClintonFrankland.Services;

/// <summary>Owns ordered, user-local transaction rules. Amount bounds compare the stored signed amount (expenses are negative).</summary>
public class TransactionRulesDataService
{
    private readonly ClintonFranklandDbContext _db;
    public TransactionRulesDataService(ClintonFranklandDbContext db) => _db = db;

    public Task<List<TransactionRule>> GetRulesAsync(int userId) => _db.Set<TransactionRule>().AsNoTracking()
        .Where(r => r.UserId == userId).OrderBy(r => r.Priority).ThenBy(r => r.TransactionRuleId).ToListAsync();

    public async Task SaveAsync(int userId, TransactionRuleDraft draft)
    {
        Validate(draft);
        var rule = draft.TransactionRuleId is int id
            ? await _db.Set<TransactionRule>().SingleOrDefaultAsync(r => r.TransactionRuleId == id && r.UserId == userId)
            : null;
        if (draft.TransactionRuleId.HasValue && rule is null) throw new InvalidOperationException("Rule was not found.");
        if (rule is null) { rule = new TransactionRule { UserId = userId, Priority = (await _db.Set<TransactionRule>().Where(r => r.UserId == userId).MaxAsync(r => (int?)r.Priority) ?? -1) + 1 }; _db.Add(rule); }
        rule.ContainsText = draft.ContainsText.Trim(); rule.AccountId = draft.AccountId; rule.MinimumAmount = draft.MinimumAmount; rule.MaximumAmount = draft.MaximumAmount;
        rule.CategoryName = Trim(draft.CategoryName); rule.PayeeName = Trim(draft.PayeeName); rule.Notes = Trim(draft.Notes); rule.IsEnabled = draft.IsEnabled; rule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int userId, int id) { var rule = await _db.Set<TransactionRule>().SingleOrDefaultAsync(r => r.TransactionRuleId == id && r.UserId == userId); if (rule is not null) { _db.Remove(rule); await _db.SaveChangesAsync(); } }
    public async Task ReorderAsync(int userId, IReadOnlyList<int> orderedIds) { var rules = await _db.Set<TransactionRule>().Where(r => r.UserId == userId).ToListAsync(); if (rules.Count != orderedIds.Count || !rules.Select(r => r.TransactionRuleId).OrderBy(x => x).SequenceEqual(orderedIds.OrderBy(x => x))) throw new InvalidOperationException("The submitted rule order is invalid."); for (var i = 0; i < orderedIds.Count; i++) rules.Single(r => r.TransactionRuleId == orderedIds[i]).Priority = i; await _db.SaveChangesAsync(); }

    public async Task<TransactionRuleSuggestion?> SuggestAsync(int userId, int? accountId, decimal amount, string? payee, string? notes)
    {
        var rules = await GetRulesAsync(userId);
        var rule = rules.FirstOrDefault(r => Matches(r, accountId, amount, payee, notes));
        return rule is null ? null : new(rule.TransactionRuleId, rule.CategoryName, rule.PayeeName, rule.Notes);
    }

    public async Task<List<TransactionRulePreviewItem>> PreviewAsync(int userId)
    {
        var rules = await GetRulesAsync(userId); var transactions = await _db.Transactions.Include(t => t.Payee).Include(t => t.Category)
            .Where(t => t.UserId == userId).ToListAsync();
        return transactions.Select(t => { var r = rules.FirstOrDefault(x => Matches(x, t.AccountId, t.Amount, t.Payee?.PayeeName, t.Notes)); return r is null ? null : new TransactionRulePreviewItem(t.TransactionId, t.Payee?.PayeeName ?? "", t.Category?.CategoryName ?? "", t.Notes, r.PayeeName, r.CategoryName, r.Notes, r.TransactionRuleId); }).Where(x => x is not null).Cast<TransactionRulePreviewItem>().ToList();
    }

    public async Task<int> ApplyAsync(int userId, IReadOnlyCollection<int> transactionIds)
    {
        // SQL Server applies bulk changes atomically. The non-relational branch keeps focused in-memory tests usable.
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        var rules = await GetRulesAsync(userId); var transactions = await _db.Transactions.Include(t => t.Payee).Where(t => t.UserId == userId && transactionIds.Contains(t.TransactionId)).ToListAsync(); var changed = 0;
        foreach (var t in transactions) { var r = rules.FirstOrDefault(x => Matches(x, t.AccountId, t.Amount, t.Payee?.PayeeName, t.Notes)); if (r is null) continue; if (!string.IsNullOrWhiteSpace(r.CategoryName)) { var c = await _db.Categories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryName == r.CategoryName); if (c is not null) t.CategoryId = c.CategoryId; } if (!string.IsNullOrWhiteSpace(r.PayeeName)) { var p = await _db.Payees.FirstOrDefaultAsync(p => p.UserId == userId && p.PayeeName == r.PayeeName && !p.IsDeleted); if (p is not null) t.PayeeId = p.PayeeId; } if (r.Notes is not null) t.Notes = r.Notes; changed++; }
        await _db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return changed;
    }

    public static bool Matches(TransactionRule r, int? accountId, decimal amount, string? payee, string? notes) => r.IsEnabled &&
        (r.AccountId is null || r.AccountId == accountId) && (r.MinimumAmount is null || amount >= r.MinimumAmount) && (r.MaximumAmount is null || amount <= r.MaximumAmount) &&
        ($"{payee} {notes}").Contains(r.ContainsText, StringComparison.OrdinalIgnoreCase);
    private static void Validate(TransactionRuleDraft x) { if (string.IsNullOrWhiteSpace(x.ContainsText)) throw new InvalidOperationException("Contains text is required."); if (x.MinimumAmount > x.MaximumAmount) throw new InvalidOperationException("Minimum amount cannot exceed maximum amount."); if (string.IsNullOrWhiteSpace(x.CategoryName) && string.IsNullOrWhiteSpace(x.PayeeName) && x.Notes is null) throw new InvalidOperationException("Choose at least one value to set."); }
    private static string? Trim(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
}
