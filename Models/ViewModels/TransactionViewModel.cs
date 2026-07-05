namespace ClintonFrankland.Models;

public class TransactionViewModel
{
    public int TransactionId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsCleared { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string? Notes { get; set; }
    public bool HasAttachment { get; set; }
    public bool CanManageFinancialData { get; set; }
}
