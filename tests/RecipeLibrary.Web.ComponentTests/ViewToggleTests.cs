using Bunit;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class ViewToggleTests : ComponentTestContext
{
    [Fact]
    public void ClickingCards_InvokesViewModeChanged()
    {
        ViewToggle.ViewModeKind? mode = ViewToggle.ViewModeKind.List;
        var cut = RenderComponent<ViewToggle>(parameters => parameters
            .Add(p => p.ViewMode, ViewToggle.ViewModeKind.List)
            .Add(p => p.ViewModeChanged, EventCallback.Factory.Create<ViewToggle.ViewModeKind>(this, v => mode = v)));

        cut.Find("[data-testid='view-toggle-cards']").Click();

        Assert.Equal(ViewToggle.ViewModeKind.Cards, mode);
    }

    [Fact]
    public void ClickingList_InvokesViewModeChanged()
    {
        ViewToggle.ViewModeKind? mode = ViewToggle.ViewModeKind.Cards;
        var cut = RenderComponent<ViewToggle>(parameters => parameters
            .Add(p => p.ViewMode, ViewToggle.ViewModeKind.Cards)
            .Add(p => p.ViewModeChanged, EventCallback.Factory.Create<ViewToggle.ViewModeKind>(this, v => mode = v)));

        cut.Find("[data-testid='view-toggle-list']").Click();

        Assert.Equal(ViewToggle.ViewModeKind.List, mode);
    }
}
