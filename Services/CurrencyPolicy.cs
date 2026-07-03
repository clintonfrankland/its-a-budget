namespace ClintonFrankland.Services;

public static class CurrencyPolicy
{
    public const int Scale = 2;
    public const int StandardPrecision = 18;
    public const int TransactionPrecision = 9;
    public const string NonNegativeAmountMessage = "Amount must be a non-negative value.";
    public const string AmountTooLargeMessage = "Amount is too large.";

    // Financial rounding policy: Round Half Up (AwayFromZero), 2 decimal places.
    public static decimal Round(decimal value) => Math.Round(value, Scale, MidpointRounding.AwayFromZero);

    public static decimal? Round(decimal? value)
        => value.HasValue ? Round(value.Value) : null;

    public static bool TryValidateNonNegativeSqlAmount(decimal value, out string message, int precision = StandardPrecision)
    {
        if (value < 0)
        {
            message = NonNegativeAmountMessage;
            return false;
        }

        if (!FitsSqlDecimal(value, precision))
        {
            message = AmountTooLargeMessage;
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool FitsSqlDecimal(decimal value, int precision = StandardPrecision)
    {
        var roundedAbsoluteValue = Math.Abs(Round(value));
        return roundedAbsoluteValue <= GetSqlDecimalMax(precision, Scale);
    }

    public static decimal RoundNonNegativeSqlAmount(decimal value, int precision = StandardPrecision)
    {
        if (!TryValidateNonNegativeSqlAmount(value, out var message, precision))
            throw new InvalidOperationException(message);

        return Round(value);
    }

    public static decimal RoundSignedSqlAmount(decimal value, int precision = StandardPrecision)
    {
        if (!FitsSqlDecimal(value, precision))
            throw new InvalidOperationException(AmountTooLargeMessage);

        return Round(value);
    }

    private static decimal GetSqlDecimalMax(int precision, int scale)
    {
        if (precision <= 0 || scale < 0 || scale >= precision)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision and scale must describe a valid SQL decimal.");

        var integerDigits = precision - scale;
        var max = 1m;
        for (var i = 0; i < integerDigits; i++)
        {
            max *= 10m;
        }

        var fractionalUnit = 1m;
        for (var i = 0; i < scale; i++)
        {
            fractionalUnit /= 10m;
        }

        return max - fractionalUnit;
    }
}
