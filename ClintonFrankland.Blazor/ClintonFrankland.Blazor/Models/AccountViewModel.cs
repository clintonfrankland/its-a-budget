namespace ClintonFrankland.Models;

public class AccountViewModel
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string LastUpdated { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    public decimal MinimumPayment { get; set; }
    public decimal Balance { get; set; }
    public decimal? Ratio { get; set; }
}
