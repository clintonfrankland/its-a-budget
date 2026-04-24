namespace ClintonFrankland.Models;

public sealed class HomeDashboardSummaryAuthRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class HomeDashboardSummaryResponse
{
    public DateTime AsOfDate { get; set; }
    public decimal TodayBalance { get; set; }
    public decimal UpcomingBillsTotal { get; set; }
    public decimal SafeToSpend { get; set; }
    public DateTime SafeToSpendDate { get; set; }
    public List<DashboardBillItemViewModel> UpcomingBills { get; set; } = [];
    public List<DashboardCategorySpendViewModel> CategorySpend { get; set; } = [];
}
