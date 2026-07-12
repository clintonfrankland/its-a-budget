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

    public Task<List<TransactionRule>> GetRulesAsync(int userId) => _db.TransactionRules.AsNoTracking()
        .Where(rule => rule.UserId == userId)
        .OrderBy(rule => rule.Priority)
        .ThenBy(rule => rule.TransactionRuleId)
        .ToListAsync();

    public Task<List<Account>> GetOwnedAccountsAsync(int userId) => _db.Accounts.AsNoTracking()
        .Where(account => account.UserId == userId && account.IsDeleted != true)
        .OrderByDescending(account => account.IsDefault)
        .ThenBy(account => account.AccountName)
        .ToListAsync();

    public Task<List<string>> GetOwnedCategoryNamesAsync(int userId) => _db.Categories.AsNoTracking()
        .Where(category => category.UserId == userId && category.CategoryName != null)
        .OrderBy(category => category.CategoryName)
        .Select(category => category.CategoryName!)
        .Distinct()
        .ToListAsync();

    public Task<List<string>> GetOwnedPayeeNamesAsync(int userId) => _db.Payees.AsNoTracking()
        .Where(payee => payee.UserId == userId && !payee.IsDeleted && payee.PayeeName != null)
        .OrderBy(payee => payee.PayeeName)
        .Select(payee => payee.PayeeName!)
        .Distinct()
        .ToListAsync();

    public async Task SaveAsync(int userId, TransactionRuleDraft draft)
    {
        Validate(draft);
        if (draft.AccountId.HasValue && !await _db.Accounts.AnyAsync(account => account.AccountId == draft.AccountId && account.UserId == userId && account.IsDeleted != true))
            throw new InvalidOperationException("Choose an account you own.");
        if (!string.IsNullOrWhiteSpace(draft.CategoryName) && !await _db.Categories.AnyAsync(category => category.UserId == userId && category.CategoryName == draft.CategoryName.Trim()))
            throw new InvalidOperationException("Choose one of your existing categories.");
        if (!string.IsNullOrWhiteSpace(draft.PayeeName) && !await _db.Payees.AnyAsync(payee => payee.UserId == userId && payee.PayeeName == draft.PayeeName.Trim() && !payee.IsDeleted))
            throw new InvalidOperationException("Choose one of your existing payees.");

        var rule = draft.TransactionRuleId is int id
            ? await _db.TransactionRules.SingleOrDefaultAsync(candidate => candidate.TransactionRuleId == id && candidate.UserId == userId)
            : null;
        if (draft.TransactionRuleId.HasValue && rule is null)
            throw new InvalidOperationException("Rule was not found.");
        if (rule is null)
        {
            rule = new TransactionRule
            {
                UserId = userId,
                Priority = (await _db.TransactionRules.Where(candidate => candidate.UserId == userId).MaxAsync(candidate => (int?)candidate.Priority) ?? -1) + 1
            };
            _db.Add(rule);
        }

        rule.ContainsText = draft.ContainsText.Trim();
        rule.AccountId = draft.AccountId;
        rule.MinimumAmount = draft.MinimumAmount;
        rule.MaximumAmount = draft.MaximumAmount;
        rule.CategoryName = Trim(draft.CategoryName);
        rule.PayeeName = Trim(draft.PayeeName);
        rule.Notes = Trim(draft.Notes);
        rule.IsEnabled = draft.IsEnabled;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int userId, int id)
    {
        var rule = await _db.TransactionRules.SingleOrDefaultAsync(candidate => candidate.TransactionRuleId == id && candidate.UserId == userId);
        if (rule is null) return;
        _db.Remove(rule);
        await _db.SaveChangesAsync();
    }

    public async Task ReorderAsync(int userId, IReadOnlyList<int> orderedIds)
    {
        var rules = await _db.TransactionRules.Where(rule => rule.UserId == userId).ToListAsync();
        if (rules.Count != orderedIds.Count || !rules.Select(rule => rule.TransactionRuleId).OrderBy(id => id).SequenceEqual(orderedIds.OrderBy(id => id)))
            throw new InvalidOperationException("The submitted rule order is invalid.");
        for (var index = 0; index < orderedIds.Count; index++)
            rules.Single(rule => rule.TransactionRuleId == orderedIds[index]).Priority = index;
        await _db.SaveChangesAsync();
    }

    public async Task<TransactionRuleSuggestion?> SuggestAsync(int userId, int? accountId, decimal amount, string? payee, string? notes)
    {
        var rules = await GetRulesAsync(userId);
        var rule = rules.FirstOrDefault(candidate => Matches(candidate, accountId, amount, payee, notes));
        return rule is null ? null : new(rule.TransactionRuleId, rule.CategoryName, rule.PayeeName, rule.Notes);
    }

    public async Task<List<TransactionRulePreviewItem>> PreviewAsync(int userId)
    {
        var rules = await GetRulesAsync(userId);
        var transactions = await _db.Transactions.AsNoTracking().Include(transaction => transaction.Payee).Include(transaction => transaction.Category)
            .Where(transaction => transaction.UserId == userId)
            .ToListAsync();
        var preview = new List<TransactionRulePreviewItem>();
        foreach (var transaction in transactions)
        {
            var rule = rules.FirstOrDefault(candidate => Matches(candidate, transaction.AccountId, transaction.Amount, transaction.Payee?.PayeeName, transaction.Notes));
            if (rule is null) continue;
            var changes = GetChanges(transaction, rule);
            if (changes.Count == 0) continue;
            preview.Add(new TransactionRulePreviewItem(transaction.TransactionId, transaction.Payee?.PayeeName ?? "", transaction.Category?.CategoryName ?? "", transaction.Notes,
                rule.PayeeName, rule.CategoryName, rule.Notes, rule.TransactionRuleId, changes));
        }
        return preview;
    }

    public async Task<int> ApplyAsync(int userId, IReadOnlyCollection<int> transactionIds)
    {
        await using var transactionScope = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        var rules = await GetRulesAsync(userId);
        var transactions = await _db.Transactions.Include(transaction => transaction.Payee).Include(transaction => transaction.Category)
            .Where(transaction => transaction.UserId == userId && transactionIds.Contains(transaction.TransactionId))
            .ToListAsync();
        var changed = 0;
        foreach (var transaction in transactions)
        {
            var rule = rules.FirstOrDefault(candidate => Matches(candidate, transaction.AccountId, transaction.Amount, transaction.Payee?.PayeeName, transaction.Notes));
            if (rule is null || GetChanges(transaction, rule).Count == 0) continue;

            if (!string.IsNullOrWhiteSpace(rule.CategoryName))
            {
                var category = await _db.Categories.FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.CategoryName == rule.CategoryName);
                if (category is not null) transaction.CategoryId = category.CategoryId;
            }
            if (!string.IsNullOrWhiteSpace(rule.PayeeName))
            {
                var payee = await _db.Payees.FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.PayeeName == rule.PayeeName && !candidate.IsDeleted);
                if (payee is not null) transaction.PayeeId = payee.PayeeId;
            }
            if (rule.Notes is not null) transaction.Notes = rule.Notes;
            changed++;
        }
        await _db.SaveChangesAsync();
        if (transactionScope is not null) await transactionScope.CommitAsync();
        return changed;
    }

    public static bool Matches(TransactionRule rule, int? accountId, decimal amount, string? payee, string? notes) => rule.IsEnabled &&
        (rule.AccountId is null || rule.AccountId == accountId) &&
        (rule.MinimumAmount is null || amount >= rule.MinimumAmount) &&
        (rule.MaximumAmount is null || amount <= rule.MaximumAmount) &&
        ($"{payee} {notes}").Contains(rule.ContainsText, StringComparison.OrdinalIgnoreCase);

    private static List<string> GetChanges(Transaction transaction, TransactionRule rule)
    {
        var changes = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.CategoryName) && !string.Equals(transaction.Category?.CategoryName, rule.CategoryName, StringComparison.OrdinalIgnoreCase)) changes.Add("Category");
        if (!string.IsNullOrWhiteSpace(rule.PayeeName) && !string.Equals(transaction.Payee?.PayeeName, rule.PayeeName, StringComparison.OrdinalIgnoreCase)) changes.Add("Payee");
        if (rule.Notes is not null && !string.Equals(transaction.Notes, rule.Notes, StringComparison.Ordinal)) changes.Add("Notes");
        return changes;
    }

    private static void Validate(TransactionRuleDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.ContainsText)) throw new InvalidOperationException("Contains text is required.");
        if (draft.MinimumAmount > draft.MaximumAmount) throw new InvalidOperationException("Minimum amount cannot exceed maximum amount.");
        if (string.IsNullOrWhiteSpace(draft.CategoryName) && string.IsNullOrWhiteSpace(draft.PayeeName) && draft.Notes is null) throw new InvalidOperationException("Choose at least one value to set.");
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
