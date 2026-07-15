# Category Budgets

The Category Budgets page lets a signed-in user set monthly planned spending amounts for their own categories and compare those targets against actual expense transactions.

Open it from **Plan > Category Budgets**. Guests are redirected to sign in before budget data is shown.

## Month selector

The page defaults to the current month. Use the month selector at the top of the page to review or edit another month.

Each target is stored for the first day of the selected month, so changing May 2026 does not affect June 2026.

## Add or edit a target

Choose one of your categories, enter a non-negative planned amount, and select **Save**. Saving a category that already has a target for the selected month updates that target instead of creating a duplicate.

Use the edit action on a row to load that target into the editor. Use the delete action to remove only that selected month/category target.

## Status table

The monthly status table shows:

- Planned amount
- Actual expense spending for the selected month
- Remaining amount
- Percent used
- Alert status

Actual spending includes only the signed-in user's negative expense transactions in the selected month. Income and positive inflows are excluded. Transactions must belong to one of the signed-in user's accounts and categories.

Rows without a saved target can still appear when there is actual category spending in the month. Those rows show a planned amount of `$0.00` and are not flagged as overspent until a target is saved.

## Alert thresholds

The default warning threshold is 80 percent used. Overspent status appears when actual spending is greater than the planned amount.

Operators can change the warning threshold without code edits through configuration:

```json
"CategoryBudgetAlerts": {
  "WarningPercent": 80
}
```

`WarningPercent` must be between 1 and 100. Invalid values fail validation when the category budget service is used.
