# Modification timestamp contract and inventory

`LastUpdated` and `UpdatedAtUtc` are UTC persistence metadata. They describe when fields on that same current record last changed and are committed atomically with the change. They are not business-effective dates (`TransactionDate`, `NextDueDate`, `BudgetMonth`) or event timestamps (`CreatedAtUtc`, `LastSeenAtUtc`, `ReviewedAtUtc`, `CompletedAtUtc`). Physical deletes have no retained, queryable timestamp; soft-delete and status fields are ordinary persisted mutations and are stamped.

## Entity classification

| Entity | Classification | Modification field | Application-owned mutation paths |
|---|---|---|---|
| User | Mutable business/configuration | UpdatedAtUtc | authentication/profile/security/external identity, notification preferences, active budget, administration |
| Account | Mutable business | LastUpdated | account create/edit/default/soft delete/balance adjustment and ledger balance maintenance; `SetDefaultAccountAsync` also stamps its relational `ExecuteUpdate` |
| Budget | Mutable business | UpdatedAtUtc | budget item create/edit/delete, advance/skip, allowance and automatic schedule processing |
| Category | Mutable business | UpdatedAtUtc | category create/edit and import resolution |
| Payee | Mutable business | UpdatedAtUtc | payee create/edit/soft delete and import/rule resolution |
| Transaction | Mutable business | UpdatedAtUtc | create/edit/clear/unclear/delete, CSV import, rule application, budget occurrence, Plaid reconciliation |
| TransactionRule | Mutable configuration | UpdatedAtUtc | manual CRUD, learned-rule upsert, approval and match statistics |
| SharedBudget | Mutable configuration | UpdatedAtUtc | creation and rename |
| BudgetMember | Mutable configuration | UpdatedAtUtc | add, role/status change, removal and reactivation |
| BudgetInvite | Mutable configuration | UpdatedAtUtc | create, accept and revoke transitions |
| CategoryBudgetTarget | Mutable business | UpdatedAtUtc | monthly target create/update |
| SmtpSetting | Mutable configuration | UpdatedAtUtc | settings create/update |
| BillDueNotificationSetting | Mutable configuration | UpdatedAtUtc | settings create/update |
| PlaidItem | Mutable configuration/background | UpdatedAtUtc | link, cursor/status update and disconnect |
| PlaidAccountMapping | Mutable configuration | UpdatedAtUtc | mapping create/update |
| PlaidTransactionStaging | Mutable background | UpdatedAtUtc | sync upsert/removal and reconciliation review/link state |
| PlaidSyncRun | Mutable background event state | UpdatedAtUtc | run creation and completion/failure |
| PlaidWebhookDelivery | Mutable background queue state | UpdatedAtUtc | receipt, lease/attempt, completion/failure and retry |
| AccountType, Frequency | Lookup/reference | Excluded | seeded/read-only reference values; application has no mutation path |
| AuthLoginAudit | Append-only audit event | Excluded; AttemptedAtUtc is event time | insert only |
| NotificationSendLog | Append-only delivery event | Excluded; CreatedAtUtc is event time | insert only; unique key makes repeat sends no-op |
| MigrationError | Append-only diagnostic event | Excluded; OccurredAt is event time | insert only through raw SQL fallback |

## Enforcement and bypass inventory

`ClintonFranklandDbContext` stamps added and genuinely modified `IModificationTracked` entries in both synchronous and asynchronous save pipelines after change detection. One UTC clock value is shared by every affected record in a save. A controllable protected clock supports deterministic tests. Validation, authorization rejection and no-op calls do not put records in a modified state, so they do not advance metadata. A failed or rolled-back save cannot make its in-memory stamp queryable in the database.

Application source was inventoried for tracking bypasses. The only business-record `ExecuteUpdate` is the account-default normalization in `AccountsDataService.SetDefaultAccountAsync`; it writes `LastUpdated` in the same statement, while the selected default is stamped by the tracked pipeline in the surrounding transaction. `MigrationErrorTracker` raw SQL inserts an excluded append-only diagnostic. Startup raw SQL is guarded legacy schema bootstrap, not a business mutation. CSV imports/upserts, sharing, transaction rules, schedule/background work, notification settings, and Plaid synchronization/reconciliation/queue operations use tracked saves and therefore receive centralized stamps.

The forward-only migration uses guarded column creation for the legacy SQL Server schema, one fixed UTC backfill value for all legacy rows, then enforces non-null columns. Reapplying its guarded schema body with columns already present is supported and does not replace existing timestamps.
