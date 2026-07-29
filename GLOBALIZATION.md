# Globalization and Culture Strategy

## Overview
The It's a Budget intentionally pins culture to **en-US** (United States - English) for all server-side rendering and formatting operations. This is a product policy, not an environment setting. Development, review/dispatch, and production should all format values the same way.

## Rationale for en-US Culture

### Primary Reason: Currency Formatting
The primary driver for choosing `en-US` culture is **currency formatting requirements**:
- **Symbol**: `en-US` uses the `$` symbol (e.g., `$1,234.56`)
- **Decimal separator**: `en-US` uses a period for decimals (e.g., `1234.56`)
- **Thousands separator**: `en-US` uses commas for grouped numbers (e.g., `1,234.56`)
- **Predictable parsing and display**: financial workflows should not change separators or currency symbols when the server, container, browser, or reviewer locale changes

### Why Not Use Server-Side Localization?
While Blazor Server supports localization, this app:
1. Serves US currency workflows
2. Uses `$` currency formatting as a core UX requirement
3. Relies on predictable decimal and thousands separators in displays and exports
4. Prioritizes consistency over flexibility

Culture is not configurable through `appsettings`, environment variables, Docker compose, or per-review settings today. Any future move to configurable cultures must be deliberate, tested, and documented because it changes user-facing money formatting.

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

**Why**: Ensures that all subsequent operations (including service setup, EF Core work, string formatting, and JSON serialization) use en-US culture.

### Program.cs: RequestLocalization Middleware
```csharp
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(usCulture.Name)
        .AddSupportedCultures(usCulture.Name)
        .AddSupportedUICultures(usCulture.Name);
});

app.UseRequestLocalization();
```

**When**: Options are registered during service setup, and middleware runs after app construction before endpoint/page handling.

**Why**: Ensures requests and Blazor Server circuits receive an en-US culture context even when browser `Accept-Language`, host locale, or container defaults differ.

## Environment Consistency

### Target Environments
This strategy is designed to work consistently across:
- Development checkouts
- Review/dispatch checkouts and containers
- Production deploy checkouts and containers

### Key Principles
1. **No per-environment overrides** - culture is hardcoded to en-US everywhere.
2. **Single source of truth** - localization configuration is in `Program.cs`.
3. **Review parity** - review and dispatch environments should not alter culture to match the worker, browser, container, or host locale.
4. **Production parity** - production should use the same culture setup as development and review.

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
1. Run `dotnet test ClintonFrankland.Blazor.Tests/ClintonFrankland.Blazor.Tests.csproj --filter GlobalizationSourceTests`.
2. Run the app in any environment.
3. Navigate to any page that displays currency (e.g., Checkbook or Budget Forecast).
4. Confirm that currency is formatted with `$`, comma thousands separators, and period decimals (e.g., `$1,234.56`).
5. Check that the browser's `Accept-Language` header does not affect server-side formatting.

## Related Files
- `Program.cs` – Contains the culture pinning and RequestLocalization configuration.
- `ClintonFrankland.Blazor.Tests/GlobalizationSourceTests.cs` - Guards the source-level culture pin.
- `Services/BudgetItemsExportService.cs` - Uses en-US for budget export formatting.
- `README.md` - Links future agents to this policy.

## References
- [Microsoft.AspNetCore.Localization](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/localization)
- [CultureInfo Class](https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo)
- [Blazor Server Localization](https://docs.microsoft.com/en-us/aspnet/core/blazor/blazor-server-localization)
