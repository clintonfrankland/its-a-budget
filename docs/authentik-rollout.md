# Authentik Access Policy and Rollout

## Access Policy

Production policy:

- Authentik controls who may enter It's a Budget.
- It's a Budget controls what a signed-in person can do and which financial data they own.
- Authentik users must belong to the `budget-users` group before It's a Budget accepts the OIDC sign-in.
- Every accepted Authentik sign-in must resolve to one active `cfUsers` row by provider plus stable OIDC `sub`.
- It's a Budget admin access is still controlled by `cfUsers.IsAdmin`; Authentik group membership never grants It's a Budget admin rights by itself.

Keep local database login and the `AppSettings` fallback login available until the complete rollout checklist passes and normal local password login is intentionally disabled in a later change.

## Authentik Provider Setup

Create or verify the Authentik provider/application with these settings:

- Provider type: OpenID Connect.
- Redirect URI: `https://budget.clintandtara.com/signin-oidc`.
- Logout redirect URI: `https://budget.clintandtara.com/signout-callback-oidc`.
- Scopes: `openid`, `profile`, `email`.
- Subject: stable Authentik `sub` claim.
- Group claim: `groups`, `group`, `roles`, or standard role claims must include `budget-users`.
- Access policy: only users in the Authentik `budget-users` group can authorize the Budget provider.

Configure It's a Budget with environment variables:

```bash
Authentication__Authentik__Enabled=true
Authentication__Authentik__Authority="https://auth.clintandtara.com/application/o/budget/"
Authentication__Authentik__ClientId="<authentik-client-id>"
Authentication__Authentik__ClientSecret="<authentik-client-secret>"
Authentication__Authentik__ProviderName="authentik"
Authentication__Authentik__CallbackPath="/signin-oidc"
Authentication__Authentik__SignedOutCallbackPath="/signout-callback-oidc"
Authentication__Authentik__AllowedGroups__0="budget-users"
```

Do not commit client IDs, client secrets, SQL connection strings, fallback login passwords, or copied production `.env` files.

## Rollout Order

1. Keep DB login and `AppSettings` fallback enabled.
2. Configure the Authentik provider and It's a Budget environment variables.
3. Deploy with `Authentication__Authentik__AllowedGroups__0="budget-users"`.
4. Sign in locally with the fallback path.
5. Link Clinton's active Budget user row from **Profile > Link Authentik Account**, or use **Settings > Users** for admin-managed setup.
6. Test Authentik login on desktop.
7. Test Authentik login on mobile.
8. Test logout, browser restart, and container restart behavior.
9. Add one non-admin isolated test user, link that user's Authentik subject, and verify they cannot see Clinton's Budget data or admin settings.
10. Review logs for expected OIDC diagnostics and no token/secret leakage.
11. Only after the above passes, decide whether a later task should disable normal local password login. Keep the `AppSettings` fallback until a separate break-glass replacement exists.

## Smoke Checks

Production port `6080`:

```bash
curl -I http://localhost:6080/
curl -I http://localhost:6080/login
curl -I http://localhost:6080/auth/authentik/login
curl -I http://localhost:6080/auth/authentik/link
```

Expected production results:

- `/` returns `200`.
- `/login` returns `200`.
- `/auth/authentik/login` returns `302` to Authentik when OIDC is enabled and configured.
- `/auth/authentik/link` returns `302` to Authentik when OIDC is enabled and configured, then returns a local Budget user to `/profile/authentik-link/confirm` for explicit confirmation.
- A browser login returns to `https://budget.clintandtara.com/signin-oidc`.
- Logout returns through `/auth/logout` and lands on a rooted local path.

Review port `6075`:

```bash
curl -I http://localhost:6075/
curl -I http://localhost:6075/login
curl -I http://localhost:6075/auth/authentik/login
curl -I http://localhost:6075/auth/authentik/link
```

Expected review results:

- `/` and `/login` return `200` when the review app is running.
- `/auth/authentik/login` returns `404` when Authentik is disabled in review, or `302` when review OIDC settings are intentionally configured against a non-production provider.
- `/auth/authentik/link` follows the same disabled/enabled behavior as the login challenge.
- Review must use a non-production SQL database.

Reverse proxy checks:

- Forward `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`.
- Keep OIDC response mode as `query`.
- Ensure HTTPS scheme reaches the app so auth, correlation, and nonce cookies are secure.
- Set `proxy_buffer_size 16k;` on the It's a Budget nginx location to avoid callback failures from large encrypted auth cookies.

## Diagnostics

It's a Budget logs these OIDC decisions through the `ClintonFrankland.AuthentikOidc` logger:

- Missing required Authentik group.
- Missing stable subject claim.
- Unknown or unlinked subject.
- Remote OIDC failure before Budget user mapping.
- Successful linked-user sign-in.

Diagnostics include safe operational fields only: provider, required group names, received group count/names, Budget user ID on success, and a short SHA-256 subject fingerprint. Logs must not include OIDC tokens, client secrets, passwords, or raw subject identifiers.

## Emergency Fallback

If Authentik login blocks access:

1. Keep the app running if local fallback still works.
2. Sign in with the local database login or `AppSettings` fallback.
3. Set `Authentication__Authentik__Enabled=false` and recreate the It's a Budget container if OIDC causes a login loop or callback failure.
4. Verify `http://localhost:6080/` and `/login` return `200`.
5. Re-check nginx `proxy_buffer_size 16k;`, forwarded headers, Authentik redirect URI, group claim, and the Budget user link before re-enabling OIDC.
6. If a linked subject is wrong, use **Settings > Users** to relink or unlink after confirmation.

Do not remove the local fallback path in the same deployment that introduces Authentik. Disabling normal local password login should be a separate reviewed task after desktop, mobile, logout, restart, and non-admin isolation checks pass.
