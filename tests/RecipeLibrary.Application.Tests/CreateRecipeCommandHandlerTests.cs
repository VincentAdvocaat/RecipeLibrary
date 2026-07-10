using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class CreateRecipeCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsRecipeWithIngredientsAndSteps()
    {
        var recipeRepo = new FakeRecipeRepository();
        var ingredientRepo = new FakeIngredientRepository();
        var normalizer = new IngredientTextNormalizer();
        var matcher = new IngredientMatcher(ingredientRepo, normalizer, new IngredientSimilarityScorer());
        var sut = new CreateRecipeCommandHandler(recipeRepo, ingredientRepo, normalizer, matcher, new IngredientLineResolver(new IngredientNameParser()));

        var result = await sut.HandleAsync(new CreateRecipeCommand
        {
            Title = "Unit Test Pasta",
            Ingredients =
            [
                new CreateRecipeIngredientDto { Name = "Pasta", Unit = "Gram", Quantity = 200 },
            ],
            InstructionSteps =
            [
                new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Cook pasta." },
            ],
        });

        Assert.NotEqual(Guid.Empty, result.RecipeId);
        Assert.NotNull(recipeRepo.AddedRecipe);
        Assert.Equal("Unit Test Pasta", recipeRepo.AddedRecipe!.Title.Value);
        Assert.Single(recipeRepo.AddedRecipe.Ingredients);
        Assert.Single(recipeRepo.AddedRecipe.InstructionSteps);
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public Recipe? AddedRecipe { get; private set; }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default)
        {
            AddedRecipe = recipe;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recipe?>(null);
        public Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Recipe?>(null);
        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
        public Task UpdateAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeIngredientRepository : IIngredientRepository
    {
        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default)
        {
            return Task.FromResult(new CanonicalIngredient
            {
                Id = Guid.NewGuid(),
                CanonicalName = canonicalName,
                NormalizedName = normalizedName,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tag>>([]);
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
    }
}
