# Project: SproutPenny Budget App

C# Blazor Server application (.NET 10) with a SQL Server backend, Bootstrap 5, Font Awesome 6, Radzen.Blazor components, Chart.js, and DataTables. A personal budget and checkbook manager with bill forecasting, account tracking, and bill-due email notifications.

## ⛔ Core Rules — Non-Negotiable

These rules override everything else. Follow them every session, every task, no exceptions.

1. **Plan before you touch code.** No guessing. No "let me just try this." Write the plan first → execute second → replan if needed.
2. **Don't think alone, use subagents.** Hard problems ≠ one thread of thinking. Break it down, delegate, keep context clean.
3. **Build a self-improving system.** Every mistake → saved → reused. Get better every session instead of repeating errors. Update this file after every correction.
4. **Test every change.** After every code change: run `dotnet build` — it must pass before the task is done. There are no automated tests; verify behaviour manually.
5. **Bugs = immediate action.** No procrastination. Trace → root cause → fix. Like a real engineer, not a prompt spammer.
6. **Version bump — always.** After every task that changes any file, edit `ClintonFrankland.Blazor.csproj`. First classify the change, then apply the rule:
   - Bug fix → increment patch (third number), increment build (fourth number) in `<Version>`
   - Feature → increment minor (second number), reset patch to 0, increment build
   - Any other change (docs, tests, config) → increment build only
   - Never change major without explicit instruction.
   - Format: `major.minor.patch.build` (e.g. `5.5.0.76`)

## Build & Run

- `dotnet build` — build the solution
- `dotnet run` — run the app (default: http://localhost:5070)
- `dotnet watch` — run with hot reload
- `dotnet tool install --global dotnet-ef` — install EF CLI if missing
- `dotnet ef migrations add <Name>` — scaffold a new EF Core migration
- `dotnet ef database update` — apply pending migrations

**CLI commands (run as args):**
- `dotnet run -- backup --out ./backups` — create a SQL Server `.bak` file
- `dotnet run -- restore-smoketest --file ./backups/Db_backup_*.bak` — smoke-test restore
- `dotnet run -- restore --file ./backups/Db_backup_*.bak` — restore from backup

**Docker:**
- `docker build -t clintonfrankland:latest .`
- `docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." clintonfrankland:latest`

> **Port conflict:** if launch fails, check `ss -tlnp | grep 5070` and kill any stale `dotnet` process holding the port.

## Architecture

- **Blazor Server** — `@rendermode InteractiveServer` on all pages. All interaction via SignalR. No WASM.
- **Radzen.Blazor** — primary UI component library (forms, dialogs, data grids, charts). Do not introduce MudBlazor or any other component library.
- **Bootstrap 5.3.3** — layout utilities. Font Awesome 6.5.1 for icons.
- **Chart.js + Moment.js** — loaded via CDN in `App.razor`; used for the budget forecast chart.
- **DataTables + jQuery** — loaded via CDN; used for the checkbook transaction table.
- **EF Core 10 / SQL Server** — single database provider. Migrations applied automatically on startup via `db.Database.MigrateAsync()`. Connection string: `ConnectionStrings:DefaultConnection`.
- **Custom session auth** — `ProtectedSessionStorage` holds a `UserInfo` record. No ASP.NET Identity. `AuthService` handles login, logout, lockout, and audit logging.
- **MailKit** — SMTP email dispatch for bill-due notifications. Config stored in `cfSmtpSettings` table.
- **Background workers** — `BillDueNotificationWorker` (15-min tick, user-timezone-aware) and `DatabaseBackupWorker` (scheduled SQL backups).
- **Globalization** — server and per-request culture pinned to `en-US` for consistent `$` currency formatting. See `GLOBALIZATION.md`.

## Project Structure

```
/
├── Program.cs                              # App startup, DI registration, CLI commands, startup diagnostics
├── ClintonFrankland.Blazor.csproj          # Version (<Version>major.minor.patch.build</Version>), NuGet refs
├── appsettings.json                        # Connection string, AppSettings, AuthSecurity, logging
├── appsettings.Development.json            # Dev overrides (DB credentials)
├── Components/
│   ├── App.razor                           # HTML shell, CDN links (Bootstrap, FA, Chart.js, DataTables)
│   ├── _Imports.razor                      # Global @using and @inject shortcuts
│   ├── Routes.razor                        # Router with MainLayout
│   ├── Layout/
│   │   ├── MainLayout.razor                # Navbar, layout shell, startup diagnostics banner
│   │   └── MainLayout.razor.cs             # Code-behind (IDisposable)
│   └── Pages/                              # Routable pages (.razor + .razor.cs)
│       ├── Home.razor / Home.razor.cs       # Dashboard snapshot (balance, bills, projection)
│       ├── Login.razor / Login.razor.cs     # Login form (public)
│       ├── Checkbook.razor / *.razor.cs     # Transaction ledger
│       ├── Budget.razor / *.razor.cs        # Budget forecast & chart
│       ├── BudgetItems.razor / *.razor.cs   # Budget item CRUD
│       ├── Accounts.razor / *.razor.cs      # Account management
│       ├── Settings.razor / *.razor.cs      # Admin: SMTP, notifications, backups
│       ├── Users.razor / *.razor.cs         # Admin: user management
│       └── Profile.razor / *.razor.cs       # User preferences & timezone
├── Data/
│   └── ClintonFranklandDbContext.cs         # EF Core DbContext (all DbSets)
├── Models/
│   ├── Entities/                            # EF Core entities (cf-prefixed tables)
│   │   ├── Account.cs, AccountType.cs
│   │   ├── Budget.cs, Category.cs, Frequency.cs, Payee.cs
│   │   ├── Transaction.cs
│   │   ├── User.cs
│   │   ├── AuthLoginAudit.cs
│   │   ├── SmtpSetting.cs
│   │   ├── BillDueNotificationSetting.cs
│   │   └── NotificationSendLog.cs
│   └── ViewModels/                          # UI-facing view models
│       ├── AccountViewModel.cs
│       ├── BudgetItemViewModel.cs
│       ├── DashboardSnapshotViewModel.cs
│       └── TransactionViewModel.cs
├── Services/
│   ├── AuthService.cs                       # Login, logout, lockout, session, audit
│   ├── SiteInfoService.cs                   # Reads AppSettings config block
│   ├── EmailSenderService.cs                # SMTP dispatch via MailKit
│   ├── BillDueNotificationWorker.cs         # Hosted service: bill-due emails (15-min tick)
│   ├── AccountsDataService.cs
│   ├── CheckbookDataService.cs
│   ├── BudgetDataService.cs
│   ├── BudgetItemsDataService.cs
│   ├── DashboardDataService.cs
│   ├── DatabaseBackupService.cs             # BACKUP DATABASE via ADO.NET
│   ├── DatabaseBackupWorker.cs              # Hosted service: scheduled backups
│   ├── PasswordUtility.cs                   # Salt + hash helpers
│   ├── CurrencyPolicy.cs                    # Decimal rounding for currency
│   └── StartupDiagnosticsState.cs           # DB connectivity/migration results for admin banner
├── Migrations/                              # EF Core migrations (SQL Server dialect)
├── docs/                                    # Per-feature user guides and test plans
├── wwwroot/
│   ├── css/app.css                          # Global overrides
│   ├── js/                                  # Site-level JS
│   └── images/                              # sproutpenny.png, favicon.png
└── Dockerfile                               # Multi-stage build
```

## Key Domain Concepts

- **Accounts** — bank/credit accounts with balance, type, user ownership, soft-delete flag.
- **Transactions** — ledger entries: date, amount, payee, category, account, cleared flag, notes, optional attachment path.
- **Budget items** — recurring income/expense items with frequency, amount, next-due date, late/bill flags.
- **Categories / Payees / Frequencies** — user-owned lookup tables; all transactions and budget items reference these.
- **Dashboard snapshot** — today's balance, upcoming bills, lowest projected balance over the forecast window.
- **Bill-due notifications** — per-user opt-in, delivery time, and timezone. Deduplication: one email per user per local day. Server-wide gate in `cfBillDueNotificationSettings`.
- **Users** — manually-assigned IDs; salt+hash passwords; `IsAdmin` flag guards admin pages/features.
- **SMTP settings** — stored in DB (`cfSmtpSettings`); configurable TLS mode, FROM name, sender email.
- **Database backups** — `BACKUP DATABASE … WITH COPY_ONLY, COMPRESSION` via `DatabaseBackupService`.

## Coding Conventions

- File-scoped namespaces (`namespace X;`)
- `var` when the type is obvious from the right-hand side
- Async methods must be suffixed `Async` and return `Task` / `Task<T>`
- **Razor components use `.razor.cs` code-behind partial classes** — put all C# logic there, not in `@code` blocks in the `.razor` file. Every page and layout follows this pattern.
- EF queries: always `async`/`await` with `ToListAsync()`, `FirstOrDefaultAsync()`, etc. — never `.Result` or `.Wait()`
- Auth in components: call `AuthService.IsAuthenticated` / `AuthService.CurrentUser` — these are injected as scoped services
- Inject `IDbContextFactory<ClintonFranklandDbContext>` in services; create a new context per operation with `await using var db = await DbFactory.CreateDbContextAsync()`

## Things Claude Should NOT Do

- Don't introduce MudBlazor, Blazorise, or any UI component library other than Radzen.Blazor + Bootstrap
- Don't add NuGet packages without stating what you're adding and why
- Don't modify `appsettings.json` connection strings, `AppSettings:LoginUser`, or `AppSettings:LoginPassword`
- Don't use `@code` blocks in `.razor` files — all C# logic belongs in the `.razor.cs` code-behind
- Don't skip `dotnet build` before declaring a task done
- Don't hold an EF `DbContext` across renders or async boundaries — always use `IDbContextFactory`
- Don't change the `cf`-prefixed table naming convention for new entities
- Don't write migrations in SQLite dialect — this project uses SQL Server exclusively
- Don't send emails or trigger background workers during development without confirming first

## Gotchas & Pitfalls

- **Culture / currency** — culture is pinned to `en-US` at startup AND per-request in middleware. Do not format currency with `Thread.CurrentThread.CurrentCulture` — always use `"C"` format string with the en-US culture (or just `value.ToString("C")`). See `GLOBALIZATION.md`.
- **`IDbContextFactory`** — always `await using var db = await DbFactory.CreateDbContextAsync()`. Never inject or hold `ClintonFranklandDbContext` directly in components.
- **`ProtectedSessionStorage` async** — reading session in `OnInitializedAsync` must call `await` and handle `ProtectedSessionStorage` results returning null before the circuit hydrates.
- **Radzen data grid** — after mutating data (add/edit/delete), call `grid.Reload()` explicitly to refresh the bound collection. Radzen does not observe collection changes automatically.
- **`StateHasChanged()`** — only call explicitly when updating UI from a non-UI thread (timer callbacks, background events). Normal Radzen event callbacks and Blazor event handlers trigger re-renders automatically.
- **EF migration dialect** — all migrations are written in SQL Server types (`nvarchar(max)`, `bit`, `datetime2`). If adding columns by hand in startup (for legacy DBs), use idempotent `IF COL_LENGTH(…) IS NULL ALTER TABLE…` guards.
- **EF migration designer files** — always create both the `.cs` migration file AND the `.Designer.cs` snapshot file. Also update `ClintonFranklandDbContextModelSnapshot.cs`; otherwise the next `dotnet ef migrations add` will re-generate the same change.
- **Migrations applied at startup** — `db.Database.MigrateAsync()` runs on every boot. New migrations are picked up automatically; no manual `dotnet ef database update` needed in production.
- **Background worker timezone handling** — `BillDueNotificationWorker` converts UTC now to each user's stored timezone (IANA string, default `America/New_York`) before comparing to their `BillDueDeliveryTime`. Always use `TimeZoneInfo.FindSystemTimeZoneById` with the IANA ID.
- **Version field** — the single `<Version>major.minor.patch.build</Version>` element in `ClintonFrankland.Blazor.csproj` drives the displayed version. Bump it every task per the core rules above.
- **Admin-only pages** — Settings and Users pages check `AuthService.CurrentUser.IsAdmin`. Do not gate features with JS-only checks; the server-side guard in the code-behind is authoritative.
- **Attachment paths** — `Transaction.AttachmentPath` is a relative server path. Do not store absolute paths or URLs.

## Workflow

### Plan Mode

- Start every complex task in plan mode (shift+tab to cycle modes)
- Pour energy into the plan so the implementation can be done in one shot
- When something goes sideways, switch back to plan mode and re-plan. Don't keep pushing.

### Implementation

1. Before modifying existing code, read the relevant files first to understand current patterns
2. When adding a new feature, check if a similar pattern already exists and follow it
3. Run `dotnet build` after changes to catch compile errors before moving on
4. If modifying the data model, create a migration and confirm it applies cleanly

### Parallel Work

- For tasks that need more compute, use subagents to work in parallel
- Only one agent should edit a given file at a time
- For fully parallel workstreams, use git worktrees: `git worktree add .claude/worktrees/<name> origin/master`

### Session Management

- Use `/branch` to fork a session, or `claude --resume <session-id> --fork-session` from CLI
- Use `/btw` for side queries without interrupting the agent's current work

### Automation

- Use `/loop` to run a skill on a recurring interval (e.g., `/loop 5m /babysit`)
- Use `/schedule` to schedule Claude to run on a cron-based schedule, up to a week

## Documentation

`README.md` and `docs/` are the canonical sources of project documentation. Keep them current. `CLAUDE.md` is for coding rules and agent behaviour only — project knowledge belongs in `README.md` / `docs/`.

### Standards

- **Use Mermaid diagrams** where they add clarity: architecture overviews, user flows, entity relationships.
- **Update on every change.** When a feature, schema change, or architectural decision is made, update the relevant doc(s) in the same task — not later.
- Do not create documentation files outside `docs/`; add a new file there if no suitable one exists.

## Self-Improvement

After every correction or mistake, update this CLAUDE.md with a rule to prevent repeating it. End corrections with: "Now update CLAUDE.md so you don't make that mistake again."

<!-- Add learned rules below this line -->
