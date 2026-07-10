using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Components.Molecules;
using RecipeLibrary.Web.Models;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class RecipeIngredientEditorRowTests : ComponentTestContext
{
    [Fact]
    public void RemoveButton_IsDisabled_WhenCannotRemove()
    {
        Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        var item = new RecipeIngredientEditorItem { Name = "Gehakt", Quantity = 1, UnitName = "Gram" };

        var cut = RenderComponent<RecipeIngredientEditorRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.RowIndex, 0)
            .Add(p => p.CanRemove, false));

        var remove = cut.Find("[data-testid='ingredient-row-0-remove']");
        Assert.True(remove.HasAttribute("disabled"));
    }

    [Fact]
    public void PreparationInput_UpdatesBoundItem()
    {
        Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        var item = new RecipeIngredientEditorItem { Name = "Gehakt", Quantity = 1, UnitName = "Gram" };

        var cut = RenderComponent<RecipeIngredientEditorRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.RowIndex, 0)
            .Add(p => p.CanRemove, true));

        cut.Find("[data-testid='ingredient-row-0-preparation']").Input("ruim");

        Assert.Equal("ruim", item.Preparation);
    }

    [Fact]
    public void RemoveButton_IsEnabled_WhenCanRemove()
    {
        Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        var item = new RecipeIngredientEditorItem { Name = "Gehakt", Quantity = 1, UnitName = "Gram" };
        var removed = false;

        var cut = RenderComponent<RecipeIngredientEditorRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.RowIndex, 0)
            .Add(p => p.CanRemove, true)
            .Add(p => p.OnRemove, EventCallback.Factory.Create(this, () => removed = true)));

        cut.Find("[data-testid='ingredient-row-0-remove']").Click();

        Assert.True(removed);
    }
}
