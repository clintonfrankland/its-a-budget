# Ledger scope

`LedgerScope` is the sole balance-authority rule. A visible transaction must link to a non-deleted account and have the same ownership context as that account: shared accounts require the same readable `SharedBudgetId` and an active membership for the transaction's attributed user; personal accounts require both a null `SharedBudgetId` and the account owner. Consequently, rows attributed to a removed (including former-owner) shared-budget member are quarantined, even when their `SharedBudgetId` matches.

This explicitly quarantines legacy AccountId-linked rows with missing or conflicting scope metadata. The application does not migrate, delete, or include those rows implicitly, so deployed stored `Balance` and `ClearedBalance` values remain unchanged. They require an intentional reviewed repair.

`BeginningBalance` plus scoped transactions is display authority for Checkbook, summaries, forecasting, reports, allowance/category projections, Plaid reconciliation, and adjustment previews. The stored `Balance` and `ClearedBalance` columns are compatibility caches updated only by explicit opening-balance adjustment; a name-only edit preserves them.
