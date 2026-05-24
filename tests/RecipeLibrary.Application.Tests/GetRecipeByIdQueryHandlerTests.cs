using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetRecipeByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenRecipeNotFound()
    {
        var repo = new FakeRecipeRepository(null);
        var sut = new GetRecipeByIdQueryHandler(repo);

        var result = await sut.HandleAsync(new GetRecipeByIdQuery { RecipeId = Guid.NewGuid() });

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_MapsRecipe_WithIngredientsAndSteps()
    {
        var recipeId = Guid.NewGuid();
        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle("Test Lasagna"),
            Description = "A test recipe",
            PreparationMinutes = 30,
            CookingMinutes = 60,
            Category = RecipeCategory.Meat,
            Ingredients =
            [
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    Name = "Gehakt",
                    Preparation = "fijngehakt",
                    Quantity = new Quantity(500),
                    Unit = Unit.Gram,
                }
            ],
            InstructionSteps =
            [
                new InstructionStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    StepNumber = 1,
                    Text = "Bake it.",
                }
            ],
        };

        var repo = new FakeRecipeRepository(recipe);
        var sut = new GetRecipeByIdQueryHandler(repo);

        var result = await sut.HandleAsync(new GetRecipeByIdQuery { RecipeId = recipeId });

        Assert.NotNull(result);
        Assert.Equal("Test Lasagna", result!.Title);
        Assert.Single(result.Ingredients);
        Assert.Equal("Gehakt", result.Ingredients[0].Name);
        Assert.Equal("fijngehakt", result.Ingredients[0].Preparation);
        Assert.Equal(500, result.Ingredients[0].Quantity);
        Assert.Equal("Gram", result.Ingredients[0].Unit);
        Assert.Single(result.Steps);
        Assert.Equal("Bake it.", result.Steps[0].Text);
    }

    private sealed class FakeRecipeRepository(Recipe? recipe) : Application.Abstractions.IRecipeRepository
    {
        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(recipe is not null && recipe.Id == id ? recipe : null);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            if (recipe is not null && ids.Contains(recipe.Id))
            {
                return Task.FromResult<IReadOnlyList<Recipe>>([recipe]);
            }

            return Task.FromResult<IReadOnlyList<Recipe>>([]);
        }

        public Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>(recipe is null ? [] : [recipe]);

        public Task UpdateAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
    }
}
