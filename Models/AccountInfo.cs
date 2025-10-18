namespace ClintonFrankland
{
    public class AccountInfo
    {
        public enum eAccountType
        {
            Checking = 1,
            CreditCard = 2,
            Loan = 3,
            Taxes = 4,
            Phone = 5
        }
        public int AccountId { get; set; } = -1;
        public int UserId { get; set; } = 0;
        public string AccountName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public eAccountType AccountType { get; set; } = eAccountType.Checking;
        public decimal Balance { get; set; } = 0m;
        public decimal ClearedBalance { get; set; } = 0m;
        public decimal CreditLimit { get; set; } = 0m;
        public decimal AvailableCredit { get; set; } = 0m;
        public int DueDate { get; set; } = 1;
        public decimal MinimumPayment { get; set; } = 0m;
        public decimal InterestRate { get; set; } = 0m;
        public string WebUrl { get; set; } = "";
    }
}
