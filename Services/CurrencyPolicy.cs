namespace ClintonFrankland.Services;

public static class CurrencyPolicy
{
    // Financial rounding policy: Round Half Up (AwayFromZero), 2 decimal places.
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal? Round(decimal? value)
        => value.HasValue ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;
}
