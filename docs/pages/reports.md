# Reports

`/reports` is the single authenticated, read-only reporting destination. Unauthenticated visitors are sent to `/login?Return=%2Freports`. The former `/insights` route redirects to `/reports?view=spending`.

Reports is divided into four focused sections:

- **Overview:** monthly plan, lowest projected balance, current net worth, and links to the relevant editing workflows.
- **Spending:** Spend vs Plan, top payees, and seven-month category trends.
- **Cash Flow:** a compact starting/lowest-balance summary and the next five changes, with a link to the full interactive Forecast.
- **Net Worth:** the current total and seven month-end values.

The reporting month defaults to the current month. The cash-flow horizon defaults to 30 days and is independent of that month.

## Formulas and boundaries

- **Spend vs Plan:** spending-allowance Budget Item occurrences whose periods start in the selected calendar month versus the absolute sum of negative transactions in `[month start, next month start)`. An allowance becomes effective at the start of its first recurrence period; earlier saved `CategoryBudgetTarget` values remain the historical fallback. Plans are resolved by budget scope and category before same-named categories are combined for display. Variance is plan minus actual.
- **Category Trends:** expense-only transactions for the selected month and six preceding calendar months. Months are chronological and missing values are zero.
- **Cashflow Forecast:** the default ledger account's beginning balance plus posted transactions dated on or before today, followed by scheduled Budget Item occurrences and the unspent portion of allowance periods. Posted spending and separately scheduled expenses in the same category reduce the allowance reservation so they are not counted twice. Income stays positive and expenses negative.
- **Net Worth:** for each historical month end and today, sum every readable account's `BeginningBalance + transactions dated on or before the as-of date`. Accounts without transactions remain included and signed liability balances are preserved.

Missing, blank, deleted, or inaccessible category references are labeled **Uncategorized**. All money is rounded with `CurrencyPolicy` and rendered with the app's en-US culture.

## Scope and limitations

Every query includes personal rows owned by the signed-in user and shared-budget rows for active readable memberships. Account ownership is checked as well as transaction ownership, preventing unrelated data and labels from entering a report. The dashboard reconstructs values from current accounts, transactions, targets, and recurring items; it does not persist snapshots. Historical figures therefore reflect corrections made to historical transactions or beginning balances.

Account balance entry represents the current account balance. Saving the default ledger account rebases its opening balance against posted transactions so Home, Checkbook, Forecast, and the Reports cash-flow summary agree. Net Worth remains an all-account report.
During the one-time account-balance reconciliation migration, a nonzero account with no transactions and a zero opening balance is initialized from its saved balance. Existing accounts with ledger activity are not rewritten.
