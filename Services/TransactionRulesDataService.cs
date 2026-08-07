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

    public async Task<TransactionRuleSuggestion?> SuggestAsync(int userId, int? accountId, decimal amount, string? payee, string? notes,
        string? merchantEntityId = null)
    {
        var rules = await GetRulesAsync(userId);
        var rule = rules.FirstOrDefault(candidate => Matches(candidate, accountId, amount, payee, notes, merchantEntityId));
        if (rule is null) return null;
        await RecordUseAsync(rule.TransactionRuleId, userId);
        return new(rule.TransactionRuleId, rule.CategoryName, rule.PayeeName, rule.Notes);
    }

    /// <summary>Returns explainable proposals only; no history can enable or create a rule by itself.</summary>
    public async Task<List<LearnedTransactionRuleProposal>> GetLearnedProposalsAsync(int userId, int minimumSamples = 3, decimal minimumDominance = .80m)
    {
        minimumSamples = Math.Max(2, minimumSamples);
        var evidence = await (from staged in _db.PlaidTransactionStaging.AsNoTracking()
                              join ledger in _db.Transactions.AsNoTracking().Include(transaction => transaction.Payee).Include(transaction => transaction.Category)
                                  on staged.LinkedTransactionId equals ledger.TransactionId
                              where staged.UserId == userId && staged.ReviewState == PlaidReconciliationReviewState.Confirmed && !staged.IsRemoved && !staged.IsPending &&
                                    ledger.UserId == userId && ledger.Cleared
                              select new { staged.BudgetAccountId, staged.MerchantEntityId, staged.MerchantName, staged.Name, ledger.Amount,
                                  Payee = ledger.Payee!.PayeeName, Category = ledger.Category!.CategoryName }).ToListAsync();

        var rejectedKeys = await _db.TransactionRules.AsNoTracking()
            .Where(rule => rule.UserId == userId && rule.ApprovalState == TransactionRuleApprovalState.Rejected)
            .Select(rule => new { rule.AccountId, rule.MerchantEntityId, rule.NormalizedMerchant })
            .ToListAsync();

        return evidence.GroupBy(item => new
            {
                item.BudgetAccountId,
                MerchantEntityId = Trim(item.MerchantEntityId),
                NormalizedMerchant = string.IsNullOrWhiteSpace(Trim(item.MerchantEntityId)) ? NormalizeMerchant(item.MerchantName ?? item.Name) : null,
                MerchantKey = Trim(item.MerchantEntityId) ?? NormalizeMerchant(item.MerchantName ?? item.Name)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.MerchantKey))
            .Where(group => !rejectedKeys.Any(rejected => rejected.AccountId == group.Key.BudgetAccountId &&
                (!string.IsNullOrWhiteSpace(group.Key.MerchantEntityId)
                    ? string.Equals(rejected.MerchantEntityId, group.Key.MerchantEntityId, StringComparison.Ordinal)
                    : string.Equals(rejected.NormalizedMerchant, group.Key.NormalizedMerchant, StringComparison.Ordinal))))
            .Select(group =>
            {
                var outputs = group.GroupBy(item => new { item.Payee, item.Category }).OrderByDescending(output => output.Count()).First();
                var count = group.Count(); var confidence = Math.Round((decimal)outputs.Count() / count, 4);
                var representative = group.OrderByDescending(item => !string.IsNullOrWhiteSpace(item.MerchantName)).First();
                var normalizedMerchant = group.Select(item => NormalizeMerchant(item.MerchantName ?? item.Name))
                    .Where(value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value).OrderByDescending(values => values.Count()).First().Key;
                var label = representative.MerchantName ?? representative.Name ?? normalizedMerchant;
                return new LearnedTransactionRuleProposal(label, group.Key.MerchantEntityId, normalizedMerchant, group.Key.BudgetAccountId,
                    group.Min(item => item.Amount), group.Max(item => item.Amount), outputs.Key.Category, outputs.Key.Payee, count, confidence,
                    $"{outputs.Count()} of {count} cleared, confirmed transactions agree on {outputs.Key.Payee} / {outputs.Key.Category}.",
                    group.OrderByDescending(item => item.Amount).Take(3).Select(item => $"{item.Amount:C} · {item.Payee} · {item.Category}").ToList());
            })
            .Where(proposal => proposal.MatchCount >= minimumSamples && proposal.Confidence >= minimumDominance)
            .OrderByDescending(proposal => proposal.Confidence).ThenByDescending(proposal => proposal.MatchCount).ToList();
    }

    public async Task SaveLearnedProposalAsync(int userId, LearnedTransactionRuleProposal proposal)
    {
        if (!await _db.Accounts.AnyAsync(account => account.AccountId == proposal.AccountId && account.UserId == userId && account.IsDeleted != true)) throw new InvalidOperationException("Choose an account you own.");
        if (proposal.MatchCount < 2 || proposal.Confidence < .80m) throw new InvalidOperationException("History is not strong enough to remember this rule.");
        var existing = await _db.TransactionRules.SingleOrDefaultAsync(rule => rule.UserId == userId && rule.AccountId == proposal.AccountId &&
            ((!string.IsNullOrEmpty(proposal.MerchantEntityId) && rule.MerchantEntityId == proposal.MerchantEntityId) ||
             (string.IsNullOrEmpty(proposal.MerchantEntityId) && rule.NormalizedMerchant == proposal.NormalizedMerchant)));
        if (existing is null)
        {
            existing = new TransactionRule { UserId = userId, Priority = (await _db.TransactionRules.Where(rule => rule.UserId == userId).MaxAsync(rule => (int?)rule.Priority) ?? -1) + 1 };
            _db.TransactionRules.Add(existing);
        }
        existing.ContainsText = proposal.NormalizedMerchant;
        existing.MerchantEntityId = Trim(proposal.MerchantEntityId);
        existing.NormalizedMerchant = proposal.NormalizedMerchant;
        existing.AccountId = proposal.AccountId;
        existing.MinimumAmount = proposal.MinimumAmount;
        existing.MaximumAmount = proposal.MaximumAmount;
        existing.CategoryName = proposal.CategoryName;
        existing.PayeeName = proposal.PayeeName;
        existing.Source = TransactionRuleSource.Learned;
        existing.ApprovalState = TransactionRuleApprovalState.Approved;
        existing.MatchCount = proposal.MatchCount;
        existing.Confidence = proposal.Confidence;
        existing.EvidenceSummary = proposal.EvidenceSummary;
        existing.IsEnabled = true;
        await _db.SaveChangesAsync();
    }

    public async Task RejectProposalAsync(int userId, LearnedTransactionRuleProposal proposal)
    {
        var existing = await _db.TransactionRules.SingleOrDefaultAsync(rule => rule.UserId == userId && rule.AccountId == proposal.AccountId &&
            (!string.IsNullOrWhiteSpace(proposal.MerchantEntityId)
                ? rule.MerchantEntityId == proposal.MerchantEntityId
                : rule.NormalizedMerchant == proposal.NormalizedMerchant));
        if (existing is null)
        {
            existing = new TransactionRule
            {
                UserId = userId,
                Priority = (await _db.TransactionRules.Where(rule => rule.UserId == userId).MaxAsync(rule => (int?)rule.Priority) ?? -1) + 1,
                ContainsText = proposal.NormalizedMerchant,
                MerchantEntityId = Trim(proposal.MerchantEntityId),
                NormalizedMerchant = proposal.NormalizedMerchant,
                AccountId = proposal.AccountId,
                CategoryName = proposal.CategoryName,
                PayeeName = proposal.PayeeName,
                Source = TransactionRuleSource.Learned,
                MatchCount = proposal.MatchCount,
                Confidence = proposal.Confidence,
                EvidenceSummary = proposal.EvidenceSummary
            };
            _db.TransactionRules.Add(existing);
        }
        existing.ApprovalState = TransactionRuleApprovalState.Rejected;
        existing.IsEnabled = false;
        await _db.SaveChangesAsync();
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

    public static bool Matches(TransactionRule rule, int? accountId, decimal amount, string? payee, string? notes, string? merchantEntityId = null) => rule.IsEnabled && rule.ApprovalState == TransactionRuleApprovalState.Approved &&
        (rule.AccountId is null || rule.AccountId == accountId) &&
        (rule.MinimumAmount is null || amount >= rule.MinimumAmount) &&
        (rule.MaximumAmount is null || amount <= rule.MaximumAmount) &&
        (string.IsNullOrWhiteSpace(rule.MerchantEntityId)
            ? (string.IsNullOrWhiteSpace(rule.NormalizedMerchant) ? $"{payee} {notes}".Contains(rule.ContainsText, StringComparison.OrdinalIgnoreCase) : NormalizeMerchant($"{payee} {notes}").Contains(rule.NormalizedMerchant, StringComparison.Ordinal))
            : string.Equals(rule.MerchantEntityId, merchantEntityId, StringComparison.Ordinal));

    public static string NormalizeMerchant(string? value) => string.Join(' ', new string((value ?? string.Empty).ToUpperInvariant()
        .Select(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ? character : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(token => !token.All(char.IsDigit))).Trim();

    private async Task RecordUseAsync(int ruleId, int userId)
    {
        var rule = await _db.TransactionRules.SingleOrDefaultAsync(candidate => candidate.TransactionRuleId == ruleId && candidate.UserId == userId);
        if (rule is null) return;
        rule.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

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
