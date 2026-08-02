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
        {
            // Return empty sequences instead of null so components can call .ToList() safely.
            if (typeof(TResult).IsGenericType)
            {
                var definition = typeof(TResult).GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyList<>)
                    || definition == typeof(IEnumerable<>)
                    || definition == typeof(IList<>)
                    || definition == typeof(List<>))
                {
                    var elementType = typeof(TResult).GetGenericArguments()[0];
                    var empty = Array.CreateInstance(elementType, 0);
                    return Task.FromResult((TResult)(object)empty);
                }
            }

            return Task.FromResult(default(TResult)!);
        }
    }
}
