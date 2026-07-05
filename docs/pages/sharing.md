# Sharing

The **Sharing** page lets a shared-budget Owner or Admin invite another Budget App user to the household budget.

Invites can target either:

- an email address
- an existing Budget username

The inviter chooses one of these roles:

- **Viewer**: can read shared budget data
- **Editor**: can read and manage financial data
- **Admin**: can manage financial data and shared-budget members

Invite links contain a high-entropy one-time token. Budget App stores only the token hash, never the plaintext token. Invites expire, and Owner/Admin users can revoke or resend pending invites from the same page.

The accept page requires sign-in before membership is created. It shows the inviter, shared budget, and role before the user accepts. Existing Authentik-linked users and local Budget users can accept as long as the invite target matches their Budget username or email.
