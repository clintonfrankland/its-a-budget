using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public class CurrencyPolicyTests
{
    [Theory]
    [InlineData(1.234, 1.23)]
    [InlineData(1.235, 1.24)]
    [InlineData(-1.234, -1.23)]
    [InlineData(-1.235, -1.24)]
    [InlineData(0.005, 0.01)]
    [InlineData(-0.005, -0.01)]
    public void Round_UsesTwoDecimalRoundHalfUp(double input, double expected)
    {
        Assert.Equal((decimal)expected, CurrencyPolicy.Round((decimal)input));
    }

    [Fact]
    public void TryValidateNonNegativeSqlAmount_UsesRoundedValueAndSqlPrecision()
    {
        Assert.True(CurrencyPolicy.TryValidateNonNegativeSqlAmount(9_999_999.994m, out _, CurrencyPolicy.TransactionPrecision));

        Assert.False(CurrencyPolicy.TryValidateNonNegativeSqlAmount(-0.01m, out var negativeMessage));
        Assert.Equal(CurrencyPolicy.NonNegativeAmountMessage, negativeMessage);

        Assert.False(CurrencyPolicy.TryValidateNonNegativeSqlAmount(10_000_000m, out var tooLargeMessage, CurrencyPolicy.TransactionPrecision));
        Assert.Equal(CurrencyPolicy.AmountTooLargeMessage, tooLargeMessage);
    }
}
