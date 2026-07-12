using Bunit;
using ClintonFrankland.Components;
using Microsoft.AspNetCore.Components;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetItemActionsTests : BunitContext
{
    public static TheoryData<string, string, string[]> PageLayouts => new()
    {
        { "Checkbook", "Record to Checkbook", ["Skip", "Edit", "Edit Next"] },
        { "Budget Forecast", "Edit", ["Record to Checkbook", "Skip", "Edit Next"] },
        { "Budget Items", "Edit", ["Record to Checkbook", "Skip", "Edit Next"] }
    };

    [Theory]
    [MemberData(nameof(PageLayouts))]
    public void ManageableRowRendersPrimaryAndOrderedAccessibleOverflow(string page, string primary, string[] menu)
    {
        var cut = Render(page, canManage: true);
        Assert.Equal(primary, cut.Find(".budget-item-primary").TextContent.Trim());
        Assert.Equal("More budget item actions", cut.Find(".budget-item-more").GetAttribute("aria-label"));
        Assert.Equal(menu, cut.FindAll("[role=menuitem]").Select(x => x.TextContent.Trim()));
        Assert.DoesNotContain("Mark Paid", cut.Markup);
    }

    [Theory]
    [InlineData("Checkbook")]
    [InlineData("Budget Forecast")]
    [InlineData("Budget Items")]
    public void ReadOnlyRowHidesAllActions(string page) => Assert.Empty(Render(page, false).FindAll("button"));

    [Fact]
    public void DisabledAndProgressStatesPreventActivation()
    {
        var invoked = 0;
        var cut = Render("Checkbook", true, false, true, () => invoked++);
        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        Assert.Equal("true", cut.Find(".budget-item-actions").GetAttribute("aria-busy"));
        cut.Find(".budget-item-primary").Click();
        Assert.Equal(0, invoked);
    }

    [Theory]
    [MemberData(nameof(PageLayouts))]
    public void EveryActionPathInvokesItsExistingWorkflowCallback(string page, string primary, string[] menu)
    {
        var calls = new List<string>();
        var cut = Render(page, true, onRecord: () => calls.Add("record"), onSkip: () => calls.Add("skip"),
            onEdit: () => calls.Add("edit"), onEditNext: () => calls.Add("edit-next"));
        foreach (var label in new[] { primary }.Concat(menu))
            cut.FindAll("button").Single(button => button.TextContent.Trim() == label).Click();
        Assert.Equal(page == "Checkbook" ? ["record", "skip", "edit", "edit-next"] : ["edit", "record", "skip", "edit-next"], calls);
    }

    private IRenderedComponent<BudgetItemActions> Render(string page, bool canManage, bool canRecord = true,
        bool inProgress = false, Action? onRecord = null, Action? onSkip = null, Action? onEdit = null, Action? onEditNext = null) =>
        Render<BudgetItemActions>(parameters => parameters
            .Add(x => x.Page, page).Add(x => x.CanManage, canManage).Add(x => x.CanRecordToCheckbook, canRecord)
            .Add(x => x.IsInProgress, inProgress)
            .Add(x => x.OnRecord, EventCallback.Factory.Create(this, onRecord ?? (() => { })))
            .Add(x => x.OnSkip, EventCallback.Factory.Create(this, onSkip ?? (() => { })))
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, onEdit ?? (() => { })))
            .Add(x => x.OnEditNext, EventCallback.Factory.Create(this, onEditNext ?? (() => { }))));
}
