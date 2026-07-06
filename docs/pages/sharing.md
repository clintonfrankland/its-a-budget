# Sharing

The **Sharing** page lets a shared-budget Owner or Admin manage household budget access.

Users who belong to more than one shared budget see a budget switcher in the navigation bar and on the Sharing page. Single-user budgets keep the private state simple and only show sharing controls when the user can manage that budget.

Owner/Admin users can see:

- active and removed members
- member roles and status
- pending, accepted, revoked, and expired invites
- invite expiration details

Invites can target either:

- an email address
- an existing Budget username

The inviter chooses one of these roles:

- **Viewer**: can read shared budget data
- **Editor**: can read and manage financial data
- **Admin**: can manage financial data and shared-budget members

Owner/Admin users can resend and revoke pending invites. They can also change non-owner member roles and remove non-owner members. The Owner can transfer ownership to another active member. A member can leave a shared budget when they are not the active Owner; Owners must transfer ownership before leaving.

Invite links contain a high-entropy one-time token. Budget App stores only the token hash, never the plaintext token. Invites expire, and Owner/Admin users can revoke or resend pending invites from the same page.

The accept page requires sign-in before membership is created. It shows the inviter, shared budget, and role before the user accepts. Existing Authentik-linked users and local Budget users can accept as long as the invite target matches their Budget username or email.
