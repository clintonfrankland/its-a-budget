using System.ComponentModel.DataAnnotations;

namespace ClintonFrankland.Models;

public sealed class CategoryBudgetAlertOptions
{
    public const string SectionName = "CategoryBudgetAlerts";

    [Range(1, 100)]
    public int WarningPercent { get; set; } = 80;
}
