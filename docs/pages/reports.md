# Reports

`/reports` is the single authenticated, read-only reporting destination. Unauthenticated visitors are sent to `/login?Return=%2Freports`. The former `/insights` route redirects to `/reports?view=spending`.

Reports is divided into four focused sections:

- **Overview:** monthly plan, lowest projected balance, current net worth, and links to the relevant editing workflows.
- **Spending:** planned-versus-actual income, scheduled bills, spending allowances, transfers, top payees, and seven-month category trends.
- **Cash Flow:** a compact starting/lowest-balance summary and the next five changes, with a link to the full interactive Forecast.
- **Net Worth:** the current total and seven month-end values.

The reporting month defaults to the current month and drives every part of the month-close snapshot. The cash-flow horizon defaults to 30 days and is independent of that month.

## Formulas and boundaries

- **Monthly plan:** recurring Budget Items are expanded into their actual due-date occurrences in `[month start, next month start)`. Weekly and biweekly income therefore has the real number of selected-month paydays (including three-paycheck months); scheduled expense items are reported as bills and allowances are a separate section.
- **Variance and indicator:** every row uses `Actual - Planned`. Positive income variance is **Favorable**; positive expense variance is **Unfavorable**. Overall net cash flow is actual income less actual bills and allowances, compared with the corresponding planned net cash flow and labelled **Ahead of plan** or **Behind plan**.
- **Transfers and debt:** transactions explicitly labelled as a transfer or credit-card payment (by category, payee, or note) are shown separately and excluded from spending variance. Credit-card purchases remain expenses. Mortgage and loan items are scheduled bills; when the ledger does not split principal and interest, the entire payment is consistently treated as a debt-payment bill.
- **Snapshot highlights:** total budgeted, actual, and signed variance appear above the detail. Up to three most-negative over-budget or unbudgeted categories and three largest positive unspent amounts are highlighted. The full table puts negative attention items first, then positive unspent amounts, then on-budget rows.
- **Category Trends:** expense-only transactions for the selected month and six preceding calendar months. Months are chronological and missing values are zero.
- **Cashflow Forecast:** the default ledger account's beginning balance plus cleared transactions and every uncleared Checkbook transaction, followed by scheduled Budget Item occurrences and the unspent portion of allowance periods. Uncleared transactions are treated as committed today even when their entered date is in the future. Posted spending and separately scheduled expenses in the same category reduce the allowance reservation so they are not counted twice. Income stays positive and expenses negative.
- **Net Worth:** for each historical month end and today, sum every readable account's `BeginningBalance + transactions dated on or before the as-of date`. Accounts without transactions remain included and signed liability balances are preserved.

Missing, blank, deleted, or inaccessible category references are labeled **Uncategorized**. All money is rounded with `CurrencyPolicy` and rendered with the app's en-US culture.

## Scope and limitations

Every query includes personal rows owned by the signed-in user and shared-budget rows for active readable memberships. Account ownership is checked as well as transaction ownership, preventing unrelated data and labels from entering a report. The month-close result is reconstructed on demand from current transactions, allowance Budget Items, and historical fallback targets; no month-end snapshot is persisted. Historical figures therefore reflect later corrections to those source records.

Account balance entry represents the current account balance. Saving the default ledger account rebases its opening balance against posted transactions so Home, Checkbook, Forecast, and the Reports cash-flow summary agree. Net Worth remains an all-account report.
During the one-time account-balance reconciliation migration, a nonzero account with no transactions and a zero opening balance is initialized from its saved balance. Existing accounts with ledger activity are not rewritten.
