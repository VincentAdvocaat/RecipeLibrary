using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class IngredientTagInputTests : ComponentTestContext
{
    [Fact]
    public async Task AddButton_AddsTagFromInput()
    {
        IReadOnlyList<string>? tags = [];
        Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });

        var cut = RenderComponent<IngredientTagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => tags = v)));

        cut.Find("[data-testid='tag-input']").Input("weekmenu");
        cut.Find("[data-testid='tag-add']").Click();

        Assert.Contains("weekmenu", tags ?? []);
    }
}
