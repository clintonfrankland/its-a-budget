using ClintonFrankland.Models;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public sealed class AccountBalancePresentationPolicyTests
{
    [Theory]
    [InlineData(AccountInfo.eAccountType.CreditCard)]
    [InlineData(AccountInfo.eAccountType.Loan)]
    [InlineData(AccountInfo.eAccountType.Taxes)]
    [InlineData(AccountInfo.eAccountType.Phone)]
    public void Apply_PresentsPositiveDebtBalancesAsNegative(AccountInfo.eAccountType accountType)
    {
        Assert.Equal(-125.45m, AccountBalancePresentationPolicy.Apply((int)accountType, 125.45m));
    }

    [Theory]
    [InlineData(125.45, -125.45)]
    [InlineData(-125.45, -125.45)]
    [InlineData(0, 0)]
    public void Apply_PreservesAlreadyNegativeAndZeroDebtBalances(decimal stored, decimal expected)
    {
        Assert.Equal(expected, AccountBalancePresentationPolicy.Apply((int)AccountInfo.eAccountType.Loan, stored));
    }

    [Fact]
    public void Apply_PreservesAssetBalanceSign()
    {
        Assert.Equal(125.45m, AccountBalancePresentationPolicy.Apply((int)AccountInfo.eAccountType.Checking, 125.45m));
    }
}
