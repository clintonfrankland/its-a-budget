# Settings (Server Settings)

## What this page is

The **Settings** page is the admin server-settings area and the Authentik identity status area.

Admins see server-wide settings that affect how the app behaves for everyone. Regular users can open Settings to view their own Budget user row and Authentik linked-identity status.

Regular users cannot create users, edit other users, or link/unlink Authentik identities.

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

## Users and Authentik identity links

The Users section shows each visible Budget user and their Authentik link status:

- **Provider**: normally `authentik`
- **Subject presence**: whether the stable OIDC `sub` claim is stored
- **External Email / Display Name**: provider profile details for audit context
- **Last external login**: the last successful Authentik login time recorded for that Budget user

Admins can link, relink, or unlink an Authentik identity from the Users grid. Relink and unlink actions ask for confirmation because they change which external account can sign in as that Budget user.

Regular users only see their own row and cannot change Authentik links.

### Manual first-user linking process

For Clinton's current Budget user row:

1. Sign in with the local fallback login.
2. Open **Settings**.
3. In **Users**, find Clinton's active Budget user row.
4. Click the link action.
5. Enter provider `authentik`.
6. Enter the stable Authentik subject claim for Clinton's Budget account.
7. Optionally enter the Authentik email and display name for audit context.
8. Save the link, then test **Sign in with Authentik**.

Email is displayed for context only. The Authentik subject is the durable identity key.

### Local password utility

The password utility is local-account-only. When a user is linked to Authentik, the grid labels the local password utility as disabled so it is not mistaken for an Authentik password reset.

## Why this page matters

When server email is set up correctly, the app becomes more than a register.

It becomes a gentle assistant that can tap you on the shoulder when a bill is coming up, without you having to constantly check dates.
