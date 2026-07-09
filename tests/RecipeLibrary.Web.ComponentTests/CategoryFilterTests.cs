using Bunit;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class CategoryFilterTests : ComponentTestContext
{
    [Fact]
    public void ClickingMeat_InvokesSelectedCategoryChanged()
    {
        int? selected = null;
        var cut = RenderComponent<CategoryFilter>(parameters => parameters
            .Add(p => p.SelectedCategoryChanged, EventCallback.Factory.Create<int?>(this, v => selected = v)));

        cut.Find("[data-testid='category-meat']").Click();

        Assert.Equal(2, selected);
    }
}
