# Home Dashboard Summary API

## Endpoint

- **Method:** `POST`
- **Path:** `/api/home-dashboard-summary`
- **Content-Type:** `application/json`

## Authentication behavior

The endpoint authenticates using the same credential rules and validation flow as the app login model:

1. Try `cfUsers` username/password (salt + hash verification).
2. If DB user auth does not match, still allow the configured fallback credentials from:
   - `AppSettings:LoginUser`
   - `AppSettings:LoginPassword`
3. Reuse the shared authentication behavior for lockout tracking, audit logging, and DB user `LastLogin` updates.

If authentication fails, the endpoint returns `401 Unauthorized`.

## Request payload

```json
{
  "username": "your-username",
  "password": "your-password"
}
```

## Response payload (`200 OK`)

```json
{
  "asOfDate": "2026-04-24T00:00:00",
  "todayBalance": 1234.56,
  "upcomingBillsTotal": 450.00,
  "safeToSpend": 980.12,
  "safeToSpendDate": "2026-05-03T00:00:00",
  "upcomingBills": [
    {
      "budgetId": 42,
      "name": "Electric",
      "payee": "Power Co",
      "dueDate": "2026-04-28T00:00:00",
      "amount": 125.00,
      "isPastDue": false
    }
  ],
  "categorySpend": [
    {
      "categoryName": "Groceries",
      "total": 320.50,
      "budgetedMonthly": 400.00,
      "isOnBudget": true
    }
  ]
}
```

## Field notes

- `todayBalance`: same value as Home card "Today's balance", calculated from the default ledger account's opening balance plus posted transactions.
- `upcomingBillsTotal`: sum of `upcomingBills` in the next 7 days.
- `safeToSpend`: same value as Home card "Safe-to-spend (Lowest bal)".
- `safeToSpendDate`: date when lowest projected balance occurs.
- `upcomingBills`: only bills with due date from today through today + 7 days.
- `categorySpend`: same monthly category spend projection used on Home.

No password, salt, hash, or other sensitive credential fields are ever returned.

## Manual verification steps

1. **Successful authentication**
   - Send valid credentials to `/api/home-dashboard-summary`.
   - Confirm `200 OK` and JSON payload.

2. **Failed authentication**
   - Send invalid password for a known user.
   - Confirm `401 Unauthorized`.

3. **Response payload shape**
   - Confirm JSON contains:
     - `todayBalance`
     - `upcomingBillsTotal`
     - `safeToSpend`
     - `safeToSpendDate`
     - `upcomingBills[]`
     - `categorySpend[]`

4. **7-day upcoming bills filter**
   - Create/test data with bills due:
     - within 7 days
     - after 7 days
     - in the past
   - Confirm only bills due today through next 7 days are included.

5. **UI parity spot check**
   - Log in with same user in UI.
   - Compare Home values to API response for:
     - today's balance
     - safe-to-spend
     - category spend
