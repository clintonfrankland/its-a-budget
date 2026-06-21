namespace ClintonFrankland.Models;

public class BudgetItemViewModel
{
    public int BudgetId { get; set; }
    public DateTime DueDate { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Payee { get; set; } = string.Empty;  // Optional payee override
    public string Category { get; set; } = string.Empty;
    public string FrequencyName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public bool IsBill { get; set; }
    public bool IsAuto { get; set; }
    public bool IsLate { get; set; }

    // Additional fields for BudgetItems page
    public string EndDateName { get; set; } = string.Empty;
    public decimal Monthly { get; set; }
    /// <summary>Raw transaction sums for the last 3 complete months, oldest-first. May be negative for expenses.</summary>
    public decimal[] SparklineData { get; set; } = [];
}
