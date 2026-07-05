# Sign In (Login)

## What this page is

The **Sign In** page is where you enter your username and password to access your budget data.

It’s intentionally simple: no clutter, no distractions, just a quick “let me in” so you can get back to tracking real life.

## What you’ll see

- **Sign in with Authentik** button, when Authentik login is enabled
- **Username** field
- **Password** field
- **Sign In** button

If you enter the wrong username or password, you’ll see a clear warning message: **“Invalid username or password.”**

## How to sign in

1. Tap/click **Username** and type your username.
2. Tap/click **Password** and type your password.
3. Select **Sign In**.

If everything matches, you’ll be taken into the app and you’ll be able to use the navigation bar to reach:

- Checkbook
- Budget Forecast
- Budget Items
- Accounts

When Authentik is enabled, use **Sign in with Authentik** first. The username/password form remains available as the local fallback during rollout and emergency access.

Once you are signed in, the app resolves your effective Budget user through the linked Budget account. Authentik and database logins see only data owned by that Budget user. The legacy local fallback login is the temporary exception: it uses the configured default Budget user until that break-glass path is removed.

## Tips

- If you’re on your own device, your browser may remember your username for faster sign-ins.
- If you keep getting the invalid login message, double-check spelling and capitalization.
- If Authentik says your account is not linked, an admin needs to link your Authentik subject to an active Budget App user before external login can grant access.

## Why it matters

Budgeting apps live or die on trust. This login step keeps your financial info private, which means you can confidently use the app anywhere, even on shared networks.
