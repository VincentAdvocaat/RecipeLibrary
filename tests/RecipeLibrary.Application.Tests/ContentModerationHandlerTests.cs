using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.ContentModeration;
using RecipeLibrary.Application.UseCases.RecipeImages;
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
    public async Task Create_WhenImageNeedsReview_EscalatesApprovedText()
    {
        var recipeRepo = new CreateRecipeCommandHandlerTests_Fake();
        var store = new TestContentModeration.FakeContentModerationStore();
        var imageUrl = "/api/recipe-images/flagged.png";
        store.Events.Add(new ContentModerationEvent
        {
            Id = Guid.NewGuid(),
            Kind = ContentModerationKind.Image,
            Decision = ModerationStatus.NeedsReview,
            SubjectKey = imageUrl,
            CategoriesSummary = "Sexual:2",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var approved = new ContentModerationResult(
            ModerationStatus.Approved,
            0,
            [],
            "none",
            Skipped: false);
        var sut = CreateCreateSut(
            recipeRepo,
            TestContentModeration.WithModerator(
                new TestContentModeration.FakeContentModerator(approved),
                store: store));

        var command = MinimalCreateCommand();
        command = new CreateRecipeCommand
        {
            Title = command.Title,
            ImageUrl = imageUrl,
            Ingredients = command.Ingredients,
            InstructionSteps = command.InstructionSteps,
        };

        await sut.HandleAsync(command);

        Assert.Equal(ModerationStatus.NeedsReview, recipeRepo.AddedRecipe!.ModerationStatus);
        Assert.Contains(store.Events, e => e.RecipeId == recipeRepo.AddedRecipe.Id && e.Kind == ContentModerationKind.Image);
    }

    [Fact]
    public async Task Update_WhenDisabled_PreservesExistingNeedsReview()
    {
        var recipeId = Guid.NewGuid();
        var existing = new Recipe
        {
            Id = recipeId,
            OwnerUserId = TestUserId,
            Title = new RecipeTitle("Old"),
            ModerationStatus = ModerationStatus.NeedsReview,
            ModerationSummary = "user-report",
            ModeratedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        var recipeRepo = new UpdateRecipeFake(existing);
        var sut = CreateUpdateSut(recipeRepo, TestContentModeration.Disabled());

        await sut.HandleAsync(new UpdateRecipeCommand
        {
            RecipeId = recipeId,
            Title = "New title",
            Ingredients = [new CreateRecipeIngredientDto { Name = "Water", Unit = "Milliliter", Quantity = 100 }],
            InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil." }],
        });

        Assert.Equal(ModerationStatus.NeedsReview, recipeRepo.UpdatedRecipe!.ModerationStatus);
        Assert.Equal("user-report", recipeRepo.UpdatedRecipe.ModerationSummary);
    }

    [Fact]
    public async Task Update_WhenPreviouslyRejectedAndTextApproved_RequiresReReview()
    {
        var recipeId = Guid.NewGuid();
        var existing = new Recipe
        {
            Id = recipeId,
            OwnerUserId = TestUserId,
            Title = new RecipeTitle("Bad"),
            ModerationStatus = ModerationStatus.Rejected,
            ModerationSummary = "manual:Rejected",
            ModeratedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        var recipeRepo = new UpdateRecipeFake(existing);
        var approved = new ContentModerationResult(
            ModerationStatus.Approved,
            0,
            [],
            "none",
            Skipped: false);
        var sut = CreateUpdateSut(
            recipeRepo,
            TestContentModeration.WithModerator(new TestContentModeration.FakeContentModerator(approved)));

        await sut.HandleAsync(new UpdateRecipeCommand
        {
            RecipeId = recipeId,
            Title = "Cleaned up",
            Ingredients = [new CreateRecipeIngredientDto { Name = "Water", Unit = "Milliliter", Quantity = 100 }],
            InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil." }],
        });

        Assert.Equal(ModerationStatus.NeedsReview, recipeRepo.UpdatedRecipe!.ModerationStatus);
        Assert.Equal("re-review-after-edit", recipeRepo.UpdatedRecipe.ModerationSummary);
    }

    [Fact]
    public async Task GetQueue_Throws_WhenCallerIsNotAdmin()
    {
        var sut = new GetModerationQueueQueryHandler(
            new TestContentModeration.FakeContentModerationStore(),
            new FixedCurrentUser(TestUserId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetModerationQueueQuery()));
    }

    [Fact]
    public async Task SetDecision_Throws_WhenCallerIsNotAdmin()
    {
        var sut = new SetRecipeModerationDecisionCommandHandler(
            new TestContentModeration.FakeContentModerationStore(),
            new FixedCurrentUser(TestUserId),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new SetRecipeModerationDecisionCommand
            {
                RecipeId = Guid.NewGuid(),
                Decision = nameof(ModerationStatus.Approved),
            }));
    }

    [Fact]
    public async Task Upload_WhenNeedsReview_RecordsSubjectKeyAndSaves()
    {
        using var content = new MemoryStream([0x01, 0x02]);
        var store = new TestContentModeration.FakeContentModerationStore();
        var needsReview = new ContentModerationResult(
            ModerationStatus.NeedsReview,
            2,
            [new ContentModerationCategoryScore("Sexual", 2)],
            "Sexual:2",
            Skipped: false);
        var storage = new FakeRecipeFileStorage("/api/recipe-images/review.png");
        var sut = new UploadRecipeImageCommandHandler(
            storage,
            TestContentModeration.WithModerator(
                new TestContentModeration.FakeContentModerator(needsReview),
                store: store),
            new NoOpUnitOfWork());

        var result = await sut.HandleAsync(new UploadRecipeImageCommand
        {
            Content = content,
            FileName = "review.png",
            ContentType = "image/png",
        });

        Assert.Equal("/api/recipe-images/review.png", result.Url);
        Assert.Contains(
            store.Events,
            e => e.Kind == ContentModerationKind.Image
                 && e.Decision == ModerationStatus.NeedsReview
                 && e.SubjectKey == result.Url);
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

    private static UpdateRecipeCommandHandler CreateUpdateSut(
        UpdateRecipeFake recipeRepo,
        RecipeLibrary.Application.ContentModeration.RecipeContentModerationService moderation)
    {
        var ingredientRepo = new CreatingIngredientRepository();
        var normalizer = new IngredientTextNormalizer();
        return new UpdateRecipeCommandHandler(
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

    private sealed class UpdateRecipeFake(Recipe existing) : IRecipeRepository
    {
        public Recipe? UpdatedRecipe { get; private set; }

        public Task AddAsync(Recipe recipe, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(existing.Id == id ? existing : null);

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            GetByIdAsync(ownerUserId, id, ct);

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

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default)
        {
            UpdatedRecipe = recipe;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecipeFileStorage(string url) : IRecipeFileStorage
    {
        public Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult(url);

        public Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult<(Stream, string)?>(null);
    }
}
