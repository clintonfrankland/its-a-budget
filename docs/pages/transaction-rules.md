# Transaction rules

Rules are evaluated in displayed order; the first enabled match wins. A match searches the transaction's payee and notes, and may be limited to an account and signed amount range. Expenses are negative and income is positive, so an expense range might be `-100` through `-1`.

Rules can fill category, payee, and/or notes. Category, payee, and optional account values are selected from data owned by the signed-in user. On the first Checkbook save attempt, the highest-priority enabled match is applied automatically. If it changes a value, Checkbook pauses and clearly asks the user to review or override category, payee, and notes before selecting Save again.

Preview lists only transactions whose stored values would actually change and identifies every category, payee, and notes difference. Bulk apply updates only those real changes, only for the signed-in user's transactions, and commits the selected updates in one database transaction. It uses existing categories and payees and does not silently create bulk data.
