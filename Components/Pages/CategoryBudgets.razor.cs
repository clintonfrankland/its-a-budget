using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class CategoryBudgets
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() => Navigation.NavigateTo("/budgetitems?kind=allowances", replace: true);
}
