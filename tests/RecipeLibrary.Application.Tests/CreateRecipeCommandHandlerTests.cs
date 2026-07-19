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
    private const string TestUserId = "test-user";

    [Fact]
    public async Task HandleAsync_PersistsRecipeWithIngredientsAndSteps()
    {
        var recipeRepo = new FakeRecipeRepository();
        var ingredientRepo = new FakeIngredientRepository();
        var normalizer = new IngredientTextNormalizer();
        var matcher = new IngredientMatcher(ingredientRepo, normalizer, new IngredientSimilarityScorer());
        var sut = new CreateRecipeCommandHandler(
            recipeRepo,
            ingredientRepo,
            normalizer,
            matcher,
            new IngredientLineResolver(new IngredientNameParser()),
            new FixedCurrentUser(TestUserId));

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
        Assert.Equal(TestUserId, recipeRepo.AddedRecipe.OwnerUserId);
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

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
            string ownerUserId,
            Guid recipeId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(
            string ownerUserId,
            string? search,
            RecipeCategory? category,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeIngredientRepository : IIngredientRepository
    {
        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, IReadOnlyList<string> languageCodes, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, IReadOnlyList<string> languageCodes, CancellationToken ct = default) => Task.FromResult<CanonicalIngredient?>(null);
        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, IReadOnlyList<string> languageCodes, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);
        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, IReadOnlyList<string> languageCodes, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CanonicalIngredient>>([]);

        public Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default) =>
            Task.FromResult(IngredientTestFactory.Create(displayName, languageCode));

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tag>>([]);
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
    }
}
