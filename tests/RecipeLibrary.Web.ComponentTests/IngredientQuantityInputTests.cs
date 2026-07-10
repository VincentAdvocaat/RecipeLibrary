using Bunit;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class IngredientQuantityInputTests : ComponentTestContext
{
    [Fact]
    public async Task ChangeToWholeNumber_InvokesQuantityChanged()
    {
        decimal quantity = 1;
        var cut = RenderComponent<IngredientQuantityInput>(parameters => parameters
            .Add(p => p.Quantity, quantity)
            .Add(p => p.QuantityChanged, EventCallback.Factory.Create<decimal>(this, v => quantity = v)));

        cut.Find("input").Change("3");

        Assert.Equal(3, quantity);
    }

    [Fact]
    public void IgnoresValuesBelowOne()
    {
        decimal quantity = 5;
        var cut = RenderComponent<IngredientQuantityInput>(parameters => parameters
            .Add(p => p.Quantity, quantity)
            .Add(p => p.QuantityChanged, EventCallback.Factory.Create<decimal>(this, v => quantity = v)));

        cut.Find("input").Change("0");

        Assert.Equal(5, quantity);
    }
}
