# Bill-due notifications (email) - Test plan

This is a lightweight manual test plan for the first version of bill-due email notifications.

## Preconditions

- SMTP is configured via server configuration (env/appsettings) and enabled in **Settings**.
- Bill-due notifications are enabled in **Settings**.
- At least one user has:
  - `EmailAddress` populated
  - `ReceiveBillDueNotices = true`
  - `NotificationTimezone` set to a valid timezone id
  - `NotificationDeliveryTime` set to a time earlier than now (in that timezone)
- At least one Budget item exists with:
  - `IsBill = true`
  - `NextDueDate` populated

## Cases

### 1) Due soon

- Set a bill `NextDueDate = today + N` days where `N <= DueSoonDays`.
- Wait for worker tick (up to 15 minutes), or restart the app.

Expected:
- One email is sent.
- One row in `cfNotificationSendLog` with:
  - `NoticeType = DueSoon`
  - `Status = Sent`

### 2) Due today

- Set a bill `NextDueDate = today`.

Expected:
- Email subject contains "due today".
- Log row `NoticeType = DueToday`.

### 3) Past due

- Enable past-due reminders.
- Set a bill `NextDueDate = today - 1`.

Expected:
- Email subject contains "Past due".
- Log row `NoticeType = PastDue`.

### 4) SMTP disabled

- Disable SMTP notices in Settings.

Expected:
- Worker does not send any emails.
- No new log rows.

### 5) SMTP send failure handling

- Configure an invalid SMTP host or credentials.

Expected:
- No crash loop (worker logs a warning, continues).
- One log row per bill/user/type/day with:
  - `Status = Failed`
  - `ErrorMessage` populated

### 6) Idempotency (no duplicates)

- Leave worker running.

Expected:
- Repeated ticks in the same day do not generate duplicate sends.
- Only one row exists per (UserId, BudgetId, NoticeType, NoticeLocalDate).

### 7) Shared-budget scoping

- Create two shared budgets and make a test user an active Viewer in both.
- Add due bills to both shared budgets and an unrelated bill in a third budget where the user is not an active member.
- Run the worker on Monday after the user's delivery time.

Expected:
- The test user receives only bills from the budgets where they are an active member.
- The weekly upcoming-bills summary includes the accessible budget names.
- Removed members and unrelated users receive no emails for the shared budget.
- Legacy single-user notices keep their existing per-bill subject format when only one budget is visible.
