# Reports

`/reports` is an authenticated, read-only dashboard. Unauthenticated visitors are sent to `/login?Return=%2Freports`. The reporting month defaults to the current month; the cashflow horizon defaults to 30 days and is independent of that month.

## Formulas and boundaries

- **Spend vs Plan:** saved `CategoryBudgetTarget.PlannedAmount` for the selected calendar month versus the absolute sum of negative transactions in `[month start, next month start)`. The visible union uses zero for a missing side. Variance is plan minus actual. Budget Items are not plans.
- **Category Trends:** expense-only transactions for the selected month and six preceding calendar months. Months are chronological and missing values are zero.
- **Cashflow Forecast:** all readable account beginning balances plus posted transactions dated on or before today, followed only by Budget Item occurrences after today through the inclusive horizon end. Income stays positive and expenses negative. The low point uses the earliest date when balances tie. The reporting month never changes this projection.
- **Net Worth:** for each historical month end and today, sum every readable account's `BeginningBalance + transactions dated on or before the as-of date`. Accounts without transactions remain included and signed liability balances are preserved.

Missing, blank, deleted, or inaccessible category references are labeled **Uncategorized**. All money is rounded with `CurrencyPolicy` and rendered with the app's en-US culture.

## Scope and limitations

Every query includes personal rows owned by the signed-in user and shared-budget rows for active readable memberships. Account ownership is checked as well as transaction ownership, preventing unrelated data and labels from entering a report. The dashboard reconstructs values from current accounts, transactions, targets, and recurring items; it does not persist snapshots and introduces no migration. Historical figures therefore reflect corrections made to historical transactions or beginning balances.
