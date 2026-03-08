# Settings (Server Settings)

## What this page is

The **Settings** page is for **administrators only**.

It contains server-wide settings that affect how the app behaves for everyone.

If you are not an admin, you won’t be able to access this page.

## SMTP Email (Server-wide)

This section controls how the app sends email notices (like bill-due reminders).

### Important concept: server-wide email, not per-user

- These settings are used by the **server** to send automated notices.
- Individual users do **not** enter their own SMTP passwords.

### Read-only server connection info

You may see read-only fields like:

- **Server Host**
- **Server Port**
- **Server Username**

These come from the server’s configuration (environment variables). The app intentionally does not display the SMTP password.

### Sender identity

Admins can set:

- **Sender Email**: the “From” address recipients will see
- **From Name** (optional): the friendly name shown in email clients

### Security options

- **TLS Mode**: controls how the connection to the mail server is secured.
  - STARTTLS (recommended for most providers)
  - SSL/TLS on connect (commonly used on port 465)
  - None (not recommended)

- **Allow invalid TLS certificates**: only use this in rare cases (usually internal mail servers). For normal internet email, leave it off.

### Test Recipient Email

You can set a **Test Recipient Email** address so test emails always go to a predictable inbox.

### Sending a test email (with realtime log)

When you click **Send Test Email**, the app will:

1. Try to connect to the SMTP server
2. Authenticate (if configured)
3. Send a sample email

You’ll see a **Test Email Log** on the page with step-by-step results.

This is helpful because it tells you *where* a failure happened:

- connection problem
- login/auth failure
- certificate issue
- message rejected

### Enable SMTP notices

If SMTP notices are disabled, the app will not send automated emails.

## Bill-due Email Notifications

This section controls the **server-wide behavior** of bill-due reminders.

Notes:
- The server-wide switch must be enabled here.
- Each user must also opt-in on their **Profile** page (Receive bill-due notices).
- The background worker runs inside the server process and does not require a user to have the UI open.

Options:
- **Enable bill-due email reminders**: master enable/disable.
- **Due Soon Window (days ahead)**: how far in advance to remind.
- **Send past-due reminders**: enable reminders after a due date has passed.
- **Past Due Max (days)**: safety cap to stop reminding forever.

## Why this page matters

When server email is set up correctly, the app becomes more than a register.

It becomes a gentle assistant that can tap you on the shoulder when a bill is coming up, without you having to constantly check dates.
