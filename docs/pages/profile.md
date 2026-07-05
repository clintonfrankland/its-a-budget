# Profile

## What this page is

Your **Profile** page is where you manage your personal settings for the Budget App.

Think of it as “your defaults” and “how the app should treat you”. It’s also where you can control whether you want certain reminders.

## What you can do here

### Update your basic info

Depending on your permissions and what your admin allows, you may see fields like:

- **Display Name** (how your name appears in the app)
- **Email Address** (used for certain notices and test emails)

### Notification Preferences (Bill-Due Notices)

This is where you decide if you want reminders about upcoming bills.

You can set:

- **Receive bill-due notices**
  - Turn this **on** if you want reminders.
  - Turn this **off** if you prefer to manage bills manually.

- **Timezone**
  - This makes sure reminders arrive at the right time *where you actually live*.
  - Example: `America/New_York`

- **Delivery time**
  - The time of day you prefer to receive bill reminders (your local time).
  - Example: `08:00` so you get reminders in the morning.

Tip: If you travel a lot, keeping your timezone correct prevents reminders from showing up at weird hours.

### Authentik Account

When Authentik login is enabled, Profile shows your current Authentik account status.

You can:

- **Link Authentik Account**
  - Starts Authentik sign-in with an account-link intent.
  - After Authentik returns, Budget App shows a confirmation page with the Authentik display name/email and your current Budget username.
  - The link is saved only after you confirm.

- **Relink Authentik Account**
  - Starts the same confirmation flow for replacing the linked Authentik account.

- **Unlink Authentik Account**
  - Requires confirmation and clears only your own linked Authentik identity.

Budget App never links by email alone. The stable Authentik subject is stored only after confirmation, and duplicate Authentik subjects are rejected if they already belong to another active Budget user.

## Saving changes

After making edits, choose the save/update option on the page to store your changes.

If something looks wrong (for example, an invalid timezone), the app should show a clear message so you can fix it.

## Why this page is useful

Profile settings are small, but they add up:

- You get notices when you want them.
- At the time you want them.
- In a way that fits your day instead of interrupting it.
