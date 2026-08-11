using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public static class AccountBalancePresentationPolicy
{
    public static decimal Apply(int accountTypeId, decimal balance) =>
        IsDebtAccount(accountTypeId) && balance > 0m ? -balance : balance;

    public static bool IsDebtAccount(int accountTypeId) => accountTypeId is
        (int)AccountInfo.eAccountType.CreditCard or
        (int)AccountInfo.eAccountType.Loan or
        (int)AccountInfo.eAccountType.Taxes or
        (int)AccountInfo.eAccountType.Phone;
}
