using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;

namespace ClintonFrankland.Services;

/// <summary>
/// Produces read-only, explainable reconciliation recommendations for posted Plaid evidence.
/// This phase never updates a ledger transaction or a Plaid staging record.
/// </summary>
public sealed class PlaidReconciliationService(ClintonFranklandDbContext database)
{
    private const int ExactDateWindowDays = 2;
    private const int ProbableDateWindowDays = 5;
    private const decimal RoundingTolerance = 0.01m;

    public async Task<IReadOnlyList<PlaidReconciliationResult>> ReconcileAsync(int userId, CancellationToken cancellationToken)
    {
        var stagedTransactions = await database.PlaidTransactionStaging.AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderBy(transaction => transaction.PlaidTransactionStagingId)
            .ToListAsync(cancellationToken);

        var ownedAccountIds = (await database.Accounts.AsNoTracking()
            .Where(account => account.UserId == userId && account.SharedBudgetId == null && account.IsDeleted != true)
            .Select(account => account.AccountId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var ledgerTransactions = await database.Transactions.AsNoTracking()
            .Include(transaction => transaction.Payee)
            .Where(transaction => transaction.UserId == userId
                && transaction.SharedBudgetId == null
                && ownedAccountIds.Contains(transaction.AccountId))
            .OrderBy(transaction => transaction.TransactionId)
            .ToListAsync(cancellationToken);

        return stagedTransactions.Select(stagedTransaction => Reconcile(
            stagedTransaction,
            stagedTransactions,
            ownedAccountIds,
            ledgerTransactions)).ToList();
    }

    public async Task<IReadOnlyList<PlaidReconciliationInboxItem>> GetInboxAsync(int userId, CancellationToken cancellationToken)
    {
        var recommendations = await ReconcileAsync(userId, cancellationToken);
        var stagedTransactions = await database.PlaidTransactionStaging.AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderBy(transaction => transaction.TransactionDate).ThenBy(transaction => transaction.PlaidTransactionStagingId)
            .ToListAsync(cancellationToken);
        var accountNames = await database.Accounts.AsNoTracking().Where(account => account.UserId == userId)
            .ToDictionaryAsync(account => account.AccountId, account => account.AccountName, cancellationToken);
        var candidateIds = recommendations.SelectMany(result => result.Candidates).Select(candidate => candidate.TransactionId).Distinct().ToList();
        var ledger = await database.Transactions.AsNoTracking().Include(transaction => transaction.Payee)
            .Where(transaction => candidateIds.Contains(transaction.TransactionId))
            .ToDictionaryAsync(transaction => transaction.TransactionId, cancellationToken);
        var recommendationByStageId = recommendations.ToDictionary(result => result.PlaidTransactionStagingId);

        return stagedTransactions.Select(stagedTransaction =>
        {
            var recommendation = recommendationByStageId[stagedTransaction.PlaidTransactionStagingId];
            var fingerprint = CreateSourceFingerprint(stagedTransaction);
            var sourceChangedAfterConfirmation = stagedTransaction.LinkedTransactionId.HasValue
                && (!string.Equals(stagedTransaction.LinkedSourceFingerprint, fingerprint, StringComparison.Ordinal) || stagedTransaction.IsRemoved);
            var group = sourceChangedAfterConfirmation ? PlaidReconciliationInboxGroup.ModifiedOrRemoved
                : stagedTransaction.IsPending ? PlaidReconciliationInboxGroup.Pending
                : recommendation.Disposition == PlaidReconciliationDisposition.HighConfidence ? PlaidReconciliationInboxGroup.Confident
                : recommendation.Disposition is PlaidReconciliationDisposition.Probable or PlaidReconciliationDisposition.Ambiguous ? PlaidReconciliationInboxGroup.ProbableOrAmbiguous
                : PlaidReconciliationInboxGroup.Unmatched;
            return new PlaidReconciliationInboxItem(
                stagedTransaction.PlaidTransactionStagingId, group, stagedTransaction.ReviewState, stagedTransaction.TransactionDate,
                -stagedTransaction.PlaidAmount, stagedTransaction.Name ?? string.Empty, stagedTransaction.MerchantName,
                accountNames.GetValueOrDefault(stagedTransaction.BudgetAccountId, "Unavailable account"), stagedTransaction.LinkedTransactionId,
                recommendation.RecommendedTransactionId, fingerprint, recommendation.RejectionReasons,
                recommendation.Candidates.Select(candidate => new PlaidReconciliationInboxCandidate(candidate.TransactionId, candidate.Confidence,
                    candidate.Evidence, ledger.TryGetValue(candidate.TransactionId, out var transaction)
                        ? $"{transaction.TransactionDate:MMM d, yyyy} · {transaction.Amount:C} · {transaction.Payee?.PayeeName ?? "(no payee)"}"
                        : "Unavailable transaction")).ToList(), sourceChangedAfterConfirmation);
        }).ToList();
    }

    public async Task<PlaidReconciliationActionResult> ConfirmAsync(int userId, int stagingId, int transactionId,
        string expectedSourceFingerprint, CancellationToken cancellationToken)
    {
        await using var transactionScope = await BeginTransactionAsync(cancellationToken);
        try
        {
            var stagedTransaction = await database.PlaidTransactionStaging.SingleOrDefaultAsync(transaction =>
                transaction.PlaidTransactionStagingId == stagingId && transaction.UserId == userId, cancellationToken);
            if (stagedTransaction is null) return PlaidReconciliationActionResult.NotFound;
            if (stagedTransaction.IsPending || stagedTransaction.IsRemoved || !string.Equals(CreateSourceFingerprint(stagedTransaction), expectedSourceFingerprint, StringComparison.Ordinal))
                return PlaidReconciliationActionResult.Stale;
            if (stagedTransaction.LinkedTransactionId.HasValue) return PlaidReconciliationActionResult.AlreadyReviewed;

            var ownedAccount = await database.Accounts.AnyAsync(account => account.AccountId == stagedTransaction.BudgetAccountId
                && account.UserId == userId && account.SharedBudgetId == null && account.IsDeleted != true, cancellationToken);
            var ledgerTransaction = await database.Transactions.SingleOrDefaultAsync(ledger => ledger.TransactionId == transactionId
                && ledger.UserId == userId && ledger.SharedBudgetId == null && ledger.AccountId == stagedTransaction.BudgetAccountId, cancellationToken);
            if (!ownedAccount || ledgerTransaction is null) return PlaidReconciliationActionResult.Unauthorized;
            if (ledgerTransaction.Cleared || await database.PlaidTransactionStaging.AnyAsync(stage =>
                stage.LinkedTransactionId == transactionId, cancellationToken)) return PlaidReconciliationActionResult.AlreadyReviewed;
            if (Math.Abs(ledgerTransaction.Amount + stagedTransaction.PlaidAmount) > RoundingTolerance
                || Math.Abs(ledgerTransaction.TransactionDate.DayNumber - stagedTransaction.TransactionDate.DayNumber) > ProbableDateWindowDays)
                return PlaidReconciliationActionResult.InvalidCandidate;

            stagedTransaction.LinkedTransactionId = transactionId;
            stagedTransaction.LinkedSourceFingerprint = CreateSourceFingerprint(stagedTransaction);
            stagedTransaction.ReviewState = PlaidReconciliationReviewState.Confirmed;
            stagedTransaction.ReviewedAtUtc = DateTime.UtcNow;
            ledgerTransaction.Cleared = true;
            ledgerTransaction.Notes = AppendPlaidLink(ledgerTransaction.Notes, stagedTransaction.PlaidTransactionId);
            await database.SaveChangesAsync(cancellationToken);
            if (transactionScope is not null) await transactionScope.CommitAsync(cancellationToken);
            return PlaidReconciliationActionResult.Confirmed;
        }
        catch (DbUpdateException)
        {
            if (transactionScope is not null) await transactionScope.RollbackAsync(cancellationToken);
            return PlaidReconciliationActionResult.AlreadyReviewed;
        }
    }

    public async Task<PlaidReconciliationActionResult> SetReviewStateAsync(int userId, int stagingId, string reviewState,
        string expectedSourceFingerprint, CancellationToken cancellationToken)
    {
        if (reviewState is not (PlaidReconciliationReviewState.Ignored or PlaidReconciliationReviewState.Deferred))
            return PlaidReconciliationActionResult.InvalidCandidate;
        var stagedTransaction = await database.PlaidTransactionStaging.SingleOrDefaultAsync(transaction =>
            transaction.PlaidTransactionStagingId == stagingId && transaction.UserId == userId, cancellationToken);
        if (stagedTransaction is null) return PlaidReconciliationActionResult.NotFound;
        if (!string.Equals(CreateSourceFingerprint(stagedTransaction), expectedSourceFingerprint, StringComparison.Ordinal))
            return PlaidReconciliationActionResult.Stale;
        if (stagedTransaction.LinkedTransactionId.HasValue) return PlaidReconciliationActionResult.AlreadyReviewed;
        stagedTransaction.ReviewState = reviewState;
        stagedTransaction.ReviewedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return PlaidReconciliationActionResult.Updated;
    }

    private static PlaidReconciliationResult Reconcile(
        PlaidTransactionStaging stagedTransaction,
        IReadOnlyCollection<PlaidTransactionStaging> allStagedTransactions,
        IReadOnlySet<int> ownedAccountIds,
        IReadOnlyCollection<Transaction> ledgerTransactions)
    {
        if (stagedTransaction.IsRemoved)
        {
            return Ineligible(stagedTransaction, "Plaid transaction was removed.");
        }

        if (stagedTransaction.IsPending)
        {
            return Ineligible(stagedTransaction, "Pending Plaid transactions cannot receive clear recommendations.");
        }

        if (!ownedAccountIds.Contains(stagedTransaction.BudgetAccountId))
        {
            return Ineligible(stagedTransaction, "Mapped Budget account is not exclusively owned by the Plaid user.");
        }

        var isPendingToPosted = !string.IsNullOrWhiteSpace(stagedTransaction.PendingTransactionId)
            && allStagedTransactions.Any(candidate => candidate.PlaidItemId == stagedTransaction.PlaidItemId
                && candidate.UserId == stagedTransaction.UserId
                && candidate.BudgetAccountId == stagedTransaction.BudgetAccountId
                && candidate.PlaidTransactionId == stagedTransaction.PendingTransactionId
                && candidate.IsPending
                && candidate.IsRemoved);
        var stagingDescription = string.Join(' ', new[] { stagedTransaction.MerchantName, stagedTransaction.Name }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var isConservativeType = HasConservativeKeyword(stagingDescription);
        var candidates = ledgerTransactions
            .Where(transaction => transaction.AccountId == stagedTransaction.BudgetAccountId)
            .Where(transaction => !transaction.Cleared || ContainsPlaidLink(transaction.Notes, stagedTransaction.PlaidTransactionId))
            .Where(transaction => ContainsPlaidLink(transaction.Notes, stagedTransaction.PlaidTransactionId)
                || Math.Abs(transaction.TransactionDate.DayNumber - stagedTransaction.TransactionDate.DayNumber) <= ProbableDateWindowDays)
            .Select(transaction => Evaluate(stagedTransaction, transaction, isPendingToPosted, isConservativeType))
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.TransactionId)
            .ToList();

        var viableCandidates = candidates.Where(candidate => candidate.RejectionReasons.Count == 0).ToList();
        var sameAmountCandidates = viableCandidates.Where(candidate => candidate.Evidence.Contains("amount-exact")
            || candidate.Evidence.Contains("amount-rounded")).ToList();
        var highConfidenceCandidates = viableCandidates.Where(candidate => candidate.Confidence >= 90).ToList();
        var hasDuplicateAmounts = sameAmountCandidates.Count > 1;
        var highConfidenceIsUnique = highConfidenceCandidates.Count == 1 && !hasDuplicateAmounts;

        if (isConservativeType)
        {
            return new PlaidReconciliationResult(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.Ambiguous,
                null, candidates, ["Transfer, cash, refund, or split-like evidence requires manual review."]);
        }

        if (highConfidenceIsUnique)
        {
            return new PlaidReconciliationResult(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.HighConfidence,
                highConfidenceCandidates[0].TransactionId, candidates, []);
        }

        if (viableCandidates.Count == 1)
        {
            return new PlaidReconciliationResult(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.Probable,
                viableCandidates[0].TransactionId, candidates, ["One plausible candidate exists, but the evidence is below the high-confidence threshold."]);
        }

        if (viableCandidates.Count > 1)
        {
            var reason = hasDuplicateAmounts
                ? "Duplicate amount candidates remain ambiguous."
                : highConfidenceCandidates.Count > 1
                    ? "More than one high-confidence candidate exists."
                    : "Evidence is insufficient for a unique high-confidence recommendation.";
            return new PlaidReconciliationResult(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.Ambiguous,
                null, candidates, [reason]);
        }

        return new PlaidReconciliationResult(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.NoMatch,
            null, candidates, ["No eligible ledger transaction matched the mapped account, amount, and date window."]);
    }

    private static PlaidReconciliationResult Ineligible(PlaidTransactionStaging stagedTransaction, string reason) =>
        new(stagedTransaction.PlaidTransactionStagingId, PlaidReconciliationDisposition.Ineligible, null, [], [reason]);

    private static PlaidReconciliationCandidate Evaluate(PlaidTransactionStaging stagedTransaction, Transaction ledgerTransaction,
        bool isPendingToPosted, bool isConservativeType)
    {
        var evidence = new List<string>();
        var rejectionReasons = new List<string>();
        var expectedLedgerAmount = -stagedTransaction.PlaidAmount;
        var amountDifference = Math.Abs(ledgerTransaction.Amount - expectedLedgerAmount);
        var dateDifference = Math.Abs(ledgerTransaction.TransactionDate.DayNumber - stagedTransaction.TransactionDate.DayNumber);
        var description = string.Join(' ', new[] { stagedTransaction.MerchantName, stagedTransaction.Name }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var normalizedDescription = Normalize(description);
        var normalizedPayee = Normalize(ledgerTransaction.Payee?.PayeeName);
        var isExactAmount = amountDifference == 0m;
        var isRoundedAmount = amountDifference <= RoundingTolerance;
        var descriptionMatches = !string.IsNullOrEmpty(normalizedDescription)
            && !string.IsNullOrEmpty(normalizedPayee)
            && (normalizedDescription.Contains(normalizedPayee, StringComparison.Ordinal) || normalizedPayee.Contains(normalizedDescription, StringComparison.Ordinal));
        var hasCheckReference = HasSharedReference(description, ledgerTransaction.Notes);
        var hasExistingPlaidLink = ContainsPlaidLink(ledgerTransaction.Notes, stagedTransaction.PlaidTransactionId);

        if (hasExistingPlaidLink) evidence.Add("existing-plaid-link");
        if (isPendingToPosted) evidence.Add("pending-to-posted-relationship");
        if (isExactAmount) evidence.Add("amount-exact");
        else if (isRoundedAmount) evidence.Add("amount-rounded");
        else if (!hasExistingPlaidLink) rejectionReasons.Add("Amount does not match Plaid's sign-correct ledger amount.");
        if (dateDifference <= ExactDateWindowDays) evidence.Add("date-near");
        else if (dateDifference <= ProbableDateWindowDays) evidence.Add("date-window");
        else if (!hasExistingPlaidLink) rejectionReasons.Add("Transaction date is outside the reconciliation window.");
        if (descriptionMatches) evidence.Add("payee-description");
        if (hasCheckReference) evidence.Add("check-reference");
        if (isConservativeType || HasConservativeKeyword(ledgerTransaction.Payee?.PayeeName) || HasConservativeKeyword(ledgerTransaction.Notes))
        {
            rejectionReasons.Add("Transfer, cash, refund, or split-like evidence requires manual review.");
        }

        var confidence = (hasExistingPlaidLink ? 100 : 0)
            + (isExactAmount ? 45 : isRoundedAmount ? 25 : 0)
            + (dateDifference <= ExactDateWindowDays ? 25 : dateDifference <= ProbableDateWindowDays ? 10 : 0)
            + (descriptionMatches ? 25 : 0)
            + (hasCheckReference ? 15 : 0)
            + (isPendingToPosted ? 5 : 0);
        return new PlaidReconciliationCandidate(ledgerTransaction.TransactionId, Math.Min(confidence, 100), evidence, rejectionReasons);
    }

    private static bool ContainsPlaidLink(string? notes, string plaidTransactionId) =>
        !string.IsNullOrWhiteSpace(notes)
        && notes.Contains($"plaid:{plaidTransactionId}", StringComparison.OrdinalIgnoreCase);

    private static bool HasSharedReference(string description, string? notes)
    {
        var descriptionReference = ExtractReference(description);
        var noteReference = ExtractReference(notes);
        return !string.IsNullOrEmpty(descriptionReference) && descriptionReference == noteReference;
    }

    private static string? ExtractReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 3 ? digits : null;
    }

    private static bool HasConservativeKeyword(string? value) =>
        !string.IsNullOrWhiteSpace(value) && new[] { "transfer", "cash", "atm", "refund", "split" }
            .Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string? value) => string.Concat((value ?? string.Empty)
        .Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public static string CreateSourceFingerprint(PlaidTransactionStaging transaction)
    {
        var source = string.Join('|', transaction.PlaidTransactionId, transaction.PlaidAccountId, transaction.PlaidAmount,
            transaction.TransactionDate, transaction.IsPending, transaction.IsRemoved, transaction.MerchantName, transaction.Name);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        database.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true
            ? null : await database.Database.BeginTransactionAsync(cancellationToken);

    private static string AppendPlaidLink(string? notes, string plaidTransactionId)
    {
        var marker = $"plaid:{plaidTransactionId}";
        return ContainsPlaidLink(notes, plaidTransactionId) ? notes ?? marker
            : string.IsNullOrWhiteSpace(notes) ? marker : $"{notes}\n{marker}";
    }
}

public enum PlaidReconciliationDisposition { HighConfidence, Probable, Ambiguous, NoMatch, Ineligible }

public sealed record PlaidReconciliationResult(int PlaidTransactionStagingId, PlaidReconciliationDisposition Disposition,
    int? RecommendedTransactionId, IReadOnlyList<PlaidReconciliationCandidate> Candidates, IReadOnlyList<string> RejectionReasons);

public sealed record PlaidReconciliationCandidate(int TransactionId, int Confidence, IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RejectionReasons);

public enum PlaidReconciliationInboxGroup { Confident, ProbableOrAmbiguous, Unmatched, Pending, ModifiedOrRemoved }
public enum PlaidReconciliationActionResult { Confirmed, Updated, NotFound, Stale, Unauthorized, AlreadyReviewed, InvalidCandidate }
public sealed record PlaidReconciliationInboxItem(int PlaidTransactionStagingId, PlaidReconciliationInboxGroup Group, string ReviewState,
    DateOnly TransactionDate, decimal SignCorrectAmount, string BankDescription, string? CleanedMerchant, string AccountName,
    int? LinkedTransactionId, int? RecommendedTransactionId, string SourceFingerprint, IReadOnlyList<string> Reasons,
    IReadOnlyList<PlaidReconciliationInboxCandidate> Candidates, bool SourceChangedAfterConfirmation);
public sealed record PlaidReconciliationInboxCandidate(int TransactionId, int Confidence, IReadOnlyList<string> Evidence, string Description);
