using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.ContentModeration;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class ContentModerationHandlerTests
{
    private const string TestUserId = "test-user";

    [Fact]
    public async Task Create_WhenEnabledAndApproved_SetsApprovedStatus()
    {
        var recipeRepo = new CreateRecipeCommandHandlerTests_Fake();
        var approved = new ContentModerationResult(
            ModerationStatus.Approved,
            0,
            [],
            "none",
            Skipped: false);
        var sut = CreateCreateSut(
            recipeRepo,
            TestContentModeration.WithModerator(new TestContentModeration.FakeContentModerator(approved)));

        await sut.HandleAsync(MinimalCreateCommand());

        Assert.Equal(ModerationStatus.Approved, recipeRepo.AddedRecipe!.ModerationStatus);
    }

    [Fact]
    public async Task Create_WhenEnabledAndBlocked_ThrowsAndDoesNotPersist()
    {
        var recipeRepo = new CreateRecipeCommandHandlerTests_Fake();
        var blocked = new ContentModerationResult(
            ModerationStatus.Rejected,
            5,
            [new ContentModerationCategoryScore("Hate", 5)],
            "Hate:5",
            Skipped: false);
        var sut = CreateCreateSut(
            recipeRepo,
            TestContentModeration.WithModerator(new TestContentModeration.FakeContentModerator(blocked)));

        await Assert.ThrowsAsync<ContentRejectedException>(() => sut.HandleAsync(MinimalCreateCommand()));
        Assert.Null(recipeRepo.AddedRecipe);
    }

    [Fact]
    public async Task Create_WhenDisabled_LeavesNotModerated()
    {
        var recipeRepo = new CreateRecipeCommandHandlerTests_Fake();
        var sut = CreateCreateSut(recipeRepo, TestContentModeration.Disabled());

        await sut.HandleAsync(MinimalCreateCommand());

        Assert.Equal(ModerationStatus.NotModerated, recipeRepo.AddedRecipe!.ModerationStatus);
    }

    [Fact]
    public void DecisionMapper_MapsSeverityBands()
    {
        Assert.Equal(ModerationStatus.Approved, ContentModerationDecisionMapper.Map(1, 4, 2));
        Assert.Equal(ModerationStatus.NeedsReview, ContentModerationDecisionMapper.Map(2, 4, 2));
        Assert.Equal(ModerationStatus.NeedsReview, ContentModerationDecisionMapper.Map(3, 4, 2));
        Assert.Equal(ModerationStatus.Rejected, ContentModerationDecisionMapper.Map(4, 4, 2));
    }

    private static CreateRecipeCommand MinimalCreateCommand() => new()
    {
        Title = "Soup",
        Ingredients = [new CreateRecipeIngredientDto { Name = "Water", Unit = "Milliliter", Quantity = 100 }],
        InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil." }],
    };

    private static CreateRecipeCommandHandler CreateCreateSut(
        CreateRecipeCommandHandlerTests_Fake recipeRepo,
        RecipeLibrary.Application.ContentModeration.RecipeContentModerationService moderation)
    {
        var ingredientRepo = new CreatingIngredientRepository();
        var normalizer = new IngredientTextNormalizer();
        return new CreateRecipeCommandHandler(
            recipeRepo,
            ingredientRepo,
            normalizer,
            new IngredientMatcher(ingredientRepo, normalizer, new IngredientSimilarityScorer()),
            new IngredientLineResolver(new IngredientNameParser()),
            new FixedCurrentUser(TestUserId),
            new NoOpUnitOfWork(),
            moderation);
    }

    private sealed class CreatingIngredientRepository : IngredientRepositoryStub
    {
        public override Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default) =>
            Task.FromResult(IngredientTestFactory.Create(displayName, languageCode));
    }

    private sealed class CreateRecipeCommandHandlerTests_Fake : IRecipeRepository
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

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
