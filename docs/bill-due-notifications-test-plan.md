# Bill-due notifications (email) - Test plan

This is a lightweight manual test plan for the first version of bill-due email notifications.

## Preconditions

- SMTP is configured via server configuration (env/appsettings) and enabled in **Settings**.
- Bill-due notifications are enabled in **Settings**.
- At least one user has:
  - `EmailAddress` populated
  - `ReceiveBillDueNotices = true` for individual due notices
  - `ReceiveWeeklyUpcomingBillDigest = true` for weekly upcoming-bills digests
  - `NotificationTimezone` set to a valid timezone id
  - `NotificationDeliveryTime` set to a time earlier than now (in that timezone)
- At least one Budget item exists with:
  - `IsBill = true`
  - `NextDueDate` populated

## Scheduling assumptions

- `NextDueDate` is a date-only local business date. Reminder classification compares only date components after converting the current UTC instant into the opted-in user's `NotificationTimezone`.
- `NotificationDeliveryTime` is evaluated in that same user-local timezone. A single worker tick can be before delivery time for one user and after delivery time for another.
- `NotificationSendLog.NoticeLocalDate` is the user's local notice date and remains the idempotency key for daily reminders.
- Blank or invalid `NotificationTimezone` values fall back to UTC.

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

### 8) Weekly opt-in and empty digest behavior

- Set one user to `ReceiveWeeklyUpcomingBillDigest = true`.
- Set another user to `ReceiveWeeklyUpcomingBillDigest = false`.
- Seed bills due in the next 7 days for both users.
- Seed a separate opted-in user with no bills due in the next 7 days.
- Run the worker on Monday after each user's delivery time.

Expected:
- The opted-in user with upcoming bills receives one `WeeklyUpcoming` email.
- The opted-out user does not receive a weekly digest.
- The opted-in user with no upcoming bills receives no weekly email and no `WeeklyUpcoming` send log.

### 9) Manual weekly preview

- Open **Profile** for a user with a valid email address.
- Click **Send Weekly Digest Preview**.

Expected:
- A digest is generated immediately from that user's accessible bills due in the next 7 days.
- The preview does not require waiting until Monday.
