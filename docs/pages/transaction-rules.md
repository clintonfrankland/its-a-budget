# Transaction rules

Rules are evaluated in displayed order; the first enabled match wins. A match searches the transaction's payee and notes, and may be limited to an account and signed amount range. Expenses are negative and income is positive, so an expense range might be `-100` through `-1`.

Rules can fill category, payee, and/or notes. Checkbook lets you apply a matching rule before saving and review the results. The preview page shows existing transactions that would change; bulk apply only updates the signed-in user's transactions, runs in one transaction, and uses existing categories/payees (it does not silently create bulk data).
