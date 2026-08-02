using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class IngredientTagInputTests : ComponentTestContext
{
    [Fact]
    public async Task AddButton_AddsTagFromInput()
    {
        IReadOnlyList<string>? tags = [];
        Services.AddScoped<IQueryBus, StubQueryBus>();

        var cut = RenderComponent<IngredientTagInput>(parameters => parameters
            .Add(p => p.Tags, tags)
            .Add(p => p.TagsChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => tags = v)));

        cut.Find("[data-testid='tag-input']").Input("weekmenu");
        cut.Find("[data-testid='tag-add']").Click();

        Assert.Contains("weekmenu", tags ?? []);
    }

    private sealed class StubQueryBus : IQueryBus
    {
        public Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
            where TQuery : IQuery<TResult>
            => Task.FromResult(default(TResult)!);
    }
}
