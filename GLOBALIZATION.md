# Globalization & Culture Strategy

## Overview
The Budget App intentionally pins culture to **en-US** (United States - English) for all server-side rendering and formatting operations. This strategy is implemented consistently across environments to ensure predictable currency formatting and user experience.

## Rationale for en-US Culture

### Primary Reason: Currency Formatting
The primary driver for choosing `en-US` culture is **currency formatting requirements**:
- **Symbol**: `en-US` uses the `$` symbol (e.g., `$1,234.56`)
- **Generic symbol**: Most cultures use a generic currency symbol like `¤` (e.g., `¤1,234.56` or `1 234,56¤`)
- **Number formatting**: `en-US` uses comma separators for thousands and period for decimals

### Why Not Use Server-Side Localization?
While Blazor Server supports localization, this app:
1. Primarily serves US-based users
2. Uses currency formatting as a core UX requirement
3. Avoids the complexity of supporting multiple languages/cultures
4. Prioritizes consistency over flexibility

## Implementation

### Program.cs: Early Culture Pinning
```csharp
// Pin the default culture early (before the app builds) so server-side currency formatting
// uses "$" instead of the generic currency symbol "¤".
var usCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = usCulture;
CultureInfo.DefaultThreadCurrentUICulture = usCulture;
CultureInfo.CurrentCulture = usCulture;
CultureInfo.CurrentUICulture = usCulture;
```

**When**: At the very beginning of `Program.cs`, before any other code executes.

**Why**: Ensures that all subsequent operations (including EF Core queries, string formatting, and JSON serialization) use en-US culture.

### Program.cs: RequestLocalization Middleware
```csharp
// Force request/circuit culture to en-US for consistent formatting in Blazor Server.
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US");
app.UseRequestLocalization(localizationOptions);
```

**When**: After app construction, before the HTTP pipeline is configured.

**Why**: Ensures that Blazor Server components and JavaScript interop receive en-US culture context for client-side formatting and i18n handling.

## Environment Consistency

### Target Environments
This strategy is designed to work consistently across:
- Development (`~/src/budget-app`)
- Production (`~/.openclaw/workspace/memory_system/deploy/.../repo`)
- Review/Dispatch (`~/.openclaw/workspace/memory_system/dispatch/.../repo`)

### Key Principles
1. **No per-environment overrides** – The culture is hardcoded to en-US everywhere.
2. **Single source of truth** – Localization configuration is in one place (`Program.cs`).
3. **Fail fast on misconfiguration** – If the culture is not en-US, the app will not format currency correctly.

## Migration Considerations

### If You Need to Change Culture
To change the culture:
1. Update the early culture pinning in `Program.cs`.
2. Update `RequestLocalizationOptions` in `Program.cs`.
3. Verify that `AddSupportedCultures` and `AddSupportedUICultures` include the new culture.
4. Update any client-side i18n libraries to match the new culture.

### Backward Compatibility
If you have legacy data or configurations that expect a different culture:
1. Document the expectation explicitly in this file.
2. Ensure the app continues to use the expected culture.
3. If necessary, add data migration scripts to normalize display strings.

## Testing

### Verification Steps
To verify that culture is correctly pinned:
1. Run the app in any environment.
2. Navigate to any page that displays currency (e.g., Transactions, Budget view).
3. Confirm that currency is formatted with `$` (e.g., `$100.00`).
4. Check the browser's Accept-Language header to ensure it does not affect server-side formatting.
5. Run `dotnet ef database update` to ensure migrations apply correctly across environments.

## Related Files
- `Program.cs` – Contains the culture pinning and RequestLocalization configuration.
- `Program.cs` – Contains EF Core migration execution logic.
- `Startup.cs` – Legacy startup file (may contain additional culture-related code; update if exists).

## References
- [Microsoft.AspNetCore.Localization](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/localization)
- [CultureInfo Class](https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo)
- [Blazor Server Localization](https://docs.microsoft.com/en-us/aspnet/core/blazor/blazor-server-localization)