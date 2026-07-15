using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Components.Pages;

public partial class Insights
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            Navigation.NavigateTo("/reports?view=spending", replace: true);
    }
}
