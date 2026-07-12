namespace ClintonFrankland.Models.ViewModels;

public sealed record TransactionRuleDraft(int? TransactionRuleId, string ContainsText, int? AccountId,
    decimal? MinimumAmount, decimal? MaximumAmount, string? CategoryName, string? PayeeName, string? Notes, bool IsEnabled);

public sealed record TransactionRuleSuggestion(int RuleId, string? CategoryName, string? PayeeName, string? Notes);

public sealed record TransactionRulePreviewItem(int TransactionId, string CurrentPayee, string CurrentCategory, string? CurrentNotes,
    string? NewPayee, string? NewCategory, string? NewNotes, int RuleId, IReadOnlyList<string> ChangedFields);
