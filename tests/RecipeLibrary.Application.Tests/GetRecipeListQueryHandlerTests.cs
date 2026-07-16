using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetRecipeListQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_FiltersBySearchAndCategory()
    {
        var meatRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = new RecipeTitle("Meat Lasagna"),
            Category = RecipeCategory.Meat,
            Ingredients = [new Ingredient { Name = "Gehakt", Quantity = new Quantity(1), Unit = Unit.Piece }],
        };
        var vegRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = new RecipeTitle("Veggie Soup"),
            Category = RecipeCategory.Vegetarian,
            Ingredients = [],
        };

        var repo = new FakeRecipeRepository([meatRecipe, vegRecipe]);
        var sut = new GetRecipeListQueryHandler(repo);

        var filtered = await sut.HandleAsync(new GetRecipeListQuery
        {
            Search = "Lasagna",
            Category = (int)RecipeCategory.Meat,
        });

        Assert.Single(filtered.Items);
        Assert.Equal("Meat Lasagna", filtered.Items[0].Title);
        Assert.Contains("Gehakt", filtered.Items[0].IngredientNames);
    }

    private sealed class FakeRecipeRepository(IReadOnlyList<Recipe> recipes) : IRecipeRepository
    {
        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(recipes.FirstOrDefault(r => r.Id == id));

        public Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(recipes.Where(r => ids.Contains(r.Id)).ToList());

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default)
        {
            IEnumerable<Recipe> query = recipes;
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Title.Value.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (category is not null)
            {
                query = query.Where(r => r.Category == category);
            }

            return Task.FromResult<IReadOnlyList<Recipe>>(query.ToList());
        }

        public Task UpdateAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
    }
}
