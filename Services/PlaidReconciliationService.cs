using ClintonFrankland.Data;
using ClintonFrankland.Models.Entities;
using Microsoft.EntityFrameworkCore;

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
                && candidate.PlaidTransactionId == stagedTransaction.PendingTransactionId
                && !candidate.IsRemoved);
        var stagingDescription = string.Join(' ', new[] { stagedTransaction.MerchantName, stagedTransaction.Name }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var isConservativeType = HasConservativeKeyword(stagingDescription);
        var candidates = ledgerTransactions
            .Where(transaction => transaction.AccountId == stagedTransaction.BudgetAccountId)
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
}

public enum PlaidReconciliationDisposition { HighConfidence, Probable, Ambiguous, NoMatch, Ineligible }

public sealed record PlaidReconciliationResult(int PlaidTransactionStagingId, PlaidReconciliationDisposition Disposition,
    int? RecommendedTransactionId, IReadOnlyList<PlaidReconciliationCandidate> Candidates, IReadOnlyList<string> RejectionReasons);

public sealed record PlaidReconciliationCandidate(int TransactionId, int Confidence, IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RejectionReasons);
