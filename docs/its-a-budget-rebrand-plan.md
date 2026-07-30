# It's a Budget Rebrand and Public Launch Plan

Last updated: 2026-07-29

## Goal

Rebrand the existing Budget App as **It's a Budget**, create the public-facing
website and supporting brand assets, update integrations and documentation, and
deploy the changes safely without renaming stable internal .NET, database, or
historical migration identifiers.

## Working Rules

- Use **It's a Budget** for human-facing product text.
- Use `its-a-budget` for machine-safe slugs where a new identifier is required.
- Preserve `ClintonFrankland` namespaces, `ClintonFranklandDbContext`, database
  objects, EF migration history, and other stable internal identifiers.
- Preserve the supplied transparent PNG as the master logo.
- Do not expose credentials or private financial information in public pages,
  screenshots, logs, or repository content.
- Complete and verify one chunk before moving to the next.
- Record anything requiring Clinton's choice, credentials, approval, or an
  external account action in **Needs Clinton**.

## Source of Truth

- Canonical repository: `/home/clinton/src/budget-app`
- Canonical remote: Mission Control project **It's a Budget**
- Private application: `https://budget.clintandtara.com`
- Public website domain: `https://itsabudget.com`
- Public support and administrative email: `admin@itsabudget.com`
- Public operator: Clinton Frankland
- Public positioning: pre-launch open-source software project
- Hosted service: not offered or planned for this launch
- Master logo received 2026-07-29:
  `FullLogo_Transparent_NoBuffer_1---ae7c5ebf-33b9-4de6-934f-4f7e894b2e38.png`
- Public display name: **It's a Budget**
- Preferred new slug: `its-a-budget`

## Chunk 1: Discovery and Plan

- [x] Confirm the canonical repository and production project through Mission
  Control.
- [x] Confirm the repository is clean and tracks the canonical Gitea remote.
- [x] Locate and inspect the supplied master logo.
- [x] Inventory current SproutPenny and Budget App references.
- [x] Create this tracked implementation plan.
- [x] Inspect routing, authentication boundaries, metadata, email behavior,
  styling, versioning, and deployment configuration.
- [x] Create the public-site architecture on `https://itsabudget.com`. The
  existing application hostname is private and must not be used as the public
  site's canonical URL.

## Chunk 2: Brand Foundation and Assets

- [x] Preserve the supplied original under a clearly named brand-assets path.
- [x] Create optimized web versions of the primary stacked logo.
- [x] Derive a compact icon mark for navigation and small surfaces.
- [x] Create favicon sizes and an application icon.
- [x] Create a square Plaid-compatible logo.
- [x] Create a horizontal header/email lockup where the source artwork permits.
- [x] Create a social sharing image.
- [x] Record the logo colors and typography guidance in a concise brand guide.
- [x] Verify transparency, dimensions, file sizes, and rendering on light and
  dark backgrounds.

## Chunk 3: Application Rebrand

- [x] Change configured and fallback site names to **It's a Budget**.
- [x] Replace homepage and navigation branding.
- [x] Update browser titles, favicon, accessibility labels, and image metadata.
- [x] Change Plaid's default `client_name` to **It's a Budget**.
- [x] Update user-facing references in errors, invitations, reconciliation,
  login/profile/help text, SMTP test messages, and email display text.
- [x] Keep security-sensitive internal group names and stable identifiers
  unchanged unless a compatibility-safe alias is appropriate.
- [x] Add regression tests for the public product name and Plaid client name.

## Chunk 4: Public Website and Trust Pages

- [x] Create reusable signed-out homepage copy without exposing
  private application data.
- [x] Add a clear product overview, benefits, workflow explanation, and calls to
  action.
- [x] Add an About/Contact page.
- [x] Add a Privacy Policy covering account data, Plaid data, cookies,
  authentication, retention, deletion, and contact.
- [x] Add Terms of Service.
- [x] Add a Security and Data Use page.
- [x] Add account/data-deletion instructions.
- [x] Add consistent footer links to all staged trust pages.
- [ ] Add canonical, Open Graph URL, and sitemap metadata after the final public
  domain is selected.
- [x] Block crawler indexing on the private application hostname.
- [x] Avoid invented corporate, mailing-address, support-email, or legal claims;
  clearly mark externally supplied details as pending when necessary.
- [x] Build the separate public site as a pre-launch open-source project, without
  implying that downloads, hosting, accounts, subscriptions, or Plaid access are
  publicly available.
- [x] Create a sanitized product preview using fictional/demo financial data
  only.

## Chunk 5: Documentation and Administration

- [x] Rebrand the root README and user guide.
- [x] Replace or archive stale SproutPenny branding notes.
- [x] Document brand assets and their intended uses.
- [x] Document required production configuration and OAuth callback behavior.
- [x] Update Mission Control's project display name if its API safely supports
  the change.
- [ ] Leave the Gitea repository slug unchanged unless Clinton explicitly wants
  a repository rename.
- [x] Increment the application version using the repository's SemVer rules.

## Chunk 6: Verification and Deployment

- [x] Run focused branding, routing, metadata, and Plaid tests.
- [x] Run the full Release test suite.
- [x] Build the production image.
- [x] Commit and push the complete canonical source change.
- [x] Deploy/recreate production from the canonical checkout.
- [x] Verify clean startup, database diagnostics, staged trust pages, authenticated
  routes, assets, and HTTP status.
- [x] Verify that no secrets or private information appear in public output.
- [x] Update this plan with commit, test, and deployment evidence.

## Chunk 7: External Launch Configuration

- [ ] Configure `itsabudget.com`, DNS, HTTPS, and redirect strategy.
- [x] Select the public support email: `admin@itsabudget.com`.
- [ ] Update Authentik application/provider display branding.
- [ ] Update Authentik callback/logout URLs if the hostname changes.
- [ ] Update nginx, monitoring, and production URLs if the hostname changes.
- [ ] Update the Plaid application profile with the name, logos, site, Privacy
  Policy, Terms, support details, and final OAuth redirect URI.
- [ ] Complete institution registration after the public identity is approved.
- [ ] Smoke-test the final Plaid OAuth flow.

## Needs Clinton

These items are not blockers for beginning the code and asset work. They may
require Clinton's choice, account access, approval, or externally verified
details before the public launch can be fully complete.

- [x] Choose the final public domain: `itsabudget.com`.
- [x] Confirm that `budget.clintandtara.com` is an internal/private application
  hostname and will not be published as the public website.
- [x] Confirm the public support email address:
  `admin@itsabudget.com`.
- [x] Confirm the public operator/legal name: Clinton Frankland.
- [x] Confirm that no public mailing address will be listed.
- [x] Confirm that the project is open-source-only and pre-launch, with no hosted
  service currently offered.
- [x] Approve sanitized screenshots made only with fictional/demo financial data.
- [ ] Approve any external-account changes that cannot be made safely through
  existing local configuration.
- [ ] Complete Plaid dashboard and institution-registration steps that require
  the account owner.
- [ ] Update the database-configured SMTP sender name and publish the final
  support mailbox after the address is chosen.
- [ ] Update Authentik application/provider display branding. The browser
  control proxy was unavailable during this run, and no local Authentik
  administration credential is configured.

## Progress Log

- **2026-07-29, Chunk 1 started:** Confirmed the canonical repository, clean
  worktree, production project record, current public hostname, and supplied
  1280×1046 transparent master logo. Initial user-facing reference inventory is
  complete. Detailed architecture and deployment inspection is next.
- **2026-07-29, Chunk 1 revised:** The existing root route has separate
  signed-out marketing and signed-in dashboard experiences, but Clinton
  clarified that `budget.clintandtara.com` is internal/private and is not
  intended for publication. The marketing and trust content remains useful
  staging material, but the public website requires a separate final domain and
  deployment. OIDC group and cookie identifiers remain internal compatibility
  contracts.
- **2026-07-29, Chunks 2–4 implementation complete:** Archived the original
  artwork and produced stacked, compact, favicon/app, horizontal, Plaid-square,
  and social assets. Added the brand guide, updated the application identity and
  Plaid client name, expanded the signed-out homepage, and added staged About,
  Contact, Privacy, Terms, Security, and Data Deletion routes with footer links,
  metadata, manifest, and crawler controls. Focused branding and Plaid tests
  pass 23/23.
- **2026-07-29, Chunk 5 in progress:** Rebranded source and canonical docs,
  removed the obsolete SproutPenny idea file and served assets, bumped version
  from 5.31.1/190 to 5.32.0/191, updated the production `SiteName`
  configuration, and renamed the Mission Control project to **It's a Budget**.
- **2026-07-29, Chunk 6 complete:** Committed and pushed canonical `main` at
  `adbccc1`. Focused branding/Plaid tests passed 23/23 and the full Release suite
  passed 293/293. Rebuilt and recreated production from
  `/home/clinton/src/budget-app`; startup and database diagnostics are clean.
  The private homepage, all six staged trust routes, manifest, robots file,
  favicon, and social image return HTTP 200 over the internal application
  hostname, and the live application reports version 5.32.0.
- **2026-07-29, Chunk 7 audited:** The application is deployed at its private
  HTTPS hostname, but the public brand site is not yet published. Final-domain
  selection, branded support mailbox, legal operator
  identity, Plaid dashboard profile/redirect/institution registration, SMTP
  sender display name, and Authentik display branding require Clinton's choice
  or account-owner access. Browser automation was unavailable because the node
  browser proxy is disabled, and no local administrative Authentik credential
  is configured.
- **2026-07-29, architecture correction:** Removed the internal hostname from
  canonical/Open Graph URL metadata, removed its sitemap, and changed
  `robots.txt` to `Disallow: /`. The internal marketing/legal pages are staged
  copy only and must not be described as the public website. Version advanced
  to 5.32.1/192 and the full Release suite passed 293/293.
