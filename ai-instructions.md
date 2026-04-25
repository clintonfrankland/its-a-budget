# Project: SproutPenny Budget App

SproutPenny Budget App is a C# Blazor Server application (.NET 10) with a SQL Server backend, Radzen.Blazor components, Bootstrap, Chart.js, DataTables, and background workers for budget notifications and backups.

## Core Rules

1. **Plan before you touch code.** No guessing. Write the plan first, execute second, re-plan when needed.
2. **Use parallel help when the task is large.** Break hard problems down and keep context clean.
3. **Make the system self-improving.** When a mistake teaches something durable, update the instructions.
4. **Test every change.** After every code change, run `dotnet build`; when automated tests exist or are added, run `dotnet test`; verify manual behavior when needed.
5. **Treat bugs as urgent.** Trace, root-cause, fix, and verify.
6. **Version bump every task.** Update `ClintonFrankland.Blazor.csproj` using the repo's four-part `<Version>` scheme before you call the work done.

## Build & Run

- `dotnet build`
- `dotnet run`
- `dotnet watch`
- `dotnet ef migrations add <Name>`
- `dotnet ef database update`
- `dotnet run -- backup --out ./backups`
- `dotnet run -- restore-smoketest --file ./backups/...`
- `dotnet run -- restore --file ./backups/...`

## Architecture

- **Blazor Server** for all interactive UI.
- **Radzen.Blazor** as the main UI component library.
- **SQL Server via EF Core** with migrations applied on startup.
- **Custom session auth** via `ProtectedSessionStorage` and `AuthService`.
- **Background workers** for bill-due notifications and database backups.

## Project Structure

```text
/
├── Program.cs
├── ClintonFrankland.Blazor.csproj
├── Components/
├── Data/
├── Models/
├── Services/
├── Migrations/
├── docs/
└── wwwroot/
```

## Key Domain Concepts

- **Accounts** hold balances and account metadata.
- **Transactions** are the ledger entries.
- **Budget items** drive recurring forecast behavior.
- **Categories, payees, and frequencies** are user-owned lookup tables.
- **SMTP settings and bill-due notifications** are persisted in the database.
- **Database backups** are a first-class operational feature.

## Coding Conventions

- Use file-scoped namespaces.
- Prefer `var` when the type is obvious.
- Keep component logic in `.razor.cs` code-behind files.
- Use async EF Core APIs only.
- Use `IDbContextFactory<ClintonFranklandDbContext>` for per-operation contexts.

## Things You Must Not Do

- Do not introduce another UI component library.
- Do not casually modify secrets or login credentials.
- Do not skip build verification.
- Do not hold DbContexts across renders.
- Do not break the `cf` table naming convention without a deliberate migration plan.

## Gotchas & Pitfalls

- Culture is pinned to `en-US` and currency formatting depends on it.
- Radzen grids need explicit reload behavior after mutations.
- Startup migrations and SQL Server-specific migration dialect matter.
- Background worker timezone handling is important.
- The four-part `<Version>` field is the displayed application version.

## Workflow

### Plan Mode
- Start complex work with a plan.
- Re-plan instead of thrashing when needed.

### Implementation
1. Read relevant files first.
2. Follow existing patterns.
3. Run `dotnet build` after changes.
4. Run `dotnet test` when tests exist or are added, then manually verify behavior where needed.

### Parallel Work
- Use parallel help for isolated subtasks.
- Only one editor should change a given file at a time.

### Session Management
- Keep side investigations out of the main implementation thread.
- Fork sessions when it improves clarity.

### Multi-Repo Work
- Be explicit when work spans Budget App and sibling repos.
- Verify assumptions before copying patterns across repos.

### Automation
- Keep backup and notification workflows documented.
- Prefer explicit, inspectable automation.

## Documentation

`README.md` and `docs/` are the canonical project docs. Keep them current in the same task that changes workflows, schema, commands, background jobs, or operational behavior.

### Standards

- Prefer diagrams where they add clarity.
- Keep docs focused on this project.
- Add new docs under `docs/` when needed.

## Self-Improvement

After every correction or durable lesson, update this file or the repo's canonical instructions so the same mistake is less likely to repeat.
