using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UpdateRecipeCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenRecipeNotFound()
    {
        var sut = CreateSut(new FakeRecipeRepository(existing: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleAsync(new UpdateRecipeCommand
            {
                RecipeId = Guid.NewGuid(),
                Title = "Updated",
                Ingredients = [new CreateRecipeIngredientDto { Name = "Pasta", Unit = "Gram", Quantity = 1 }],
                InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Step" }],
            }));
    }

    [Fact]
    public async Task HandleAsync_UpdatesExistingRecipe()
    {
        var recipeId = Guid.NewGuid();
        var existing = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle("Old title"),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var recipeRepo = new FakeRecipeRepository(existing);
        var sut = CreateSut(recipeRepo);

        var result = await sut.HandleAsync(new UpdateRecipeCommand
        {
            RecipeId = recipeId,
            Title = "New title",
            Ingredients = [new CreateRecipeIngredientDto { Name = "Pasta", Unit = "Gram", Quantity = 250 }],
            InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil." }],
        });

        Assert.Equal(recipeId, result.RecipeId);
        Assert.NotNull(recipeRepo.UpdatedRecipe);
        Assert.Equal("New title", recipeRepo.UpdatedRecipe!.Title.Value);
    }

    private static UpdateRecipeCommandHandler CreateSut(FakeRecipeRepository recipeRepo)
    {
        var ingredientRepo = new FakeIngredientRepository();
        var normalizer = new IngredientTextNormalizer();
        return new UpdateRecipeCommandHandler(
            recipeRepo,
            ingredientRepo,
            normalizer,
            new IngredientMatcher(ingredientRepo, normalizer, new IngredientSimilarityScorer()),
            new IngredientLineResolver(new IngredientNameParser()));
    }

    private sealed class FakeRecipeRepository(Recipe? existing) : IRecipeRepository
    {
        public Recipe? UpdatedRecipe { get; private set; }

        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(existing is not null && existing.Id == id ? existing : null);

        public Task UpdateAsync(Recipe recipe, CancellationToken ct = default)
        {
            UpdatedRecipe = recipe;
            return Task.CompletedTask;
        }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Recipe?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(Guid recipeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Recipe>>([]);
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
