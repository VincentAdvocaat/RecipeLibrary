using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a recipe with its ingredients and instruction steps.
/// </summary>
public sealed class Recipe
{
    public Guid Id { get; set; }

    /// <summary>Identity user who owns the personal recipe or created the household recipe.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    public Guid? HouseholdId { get; set; }

    public Guid? PublicShareKey { get; set; }

    /// <summary>Source identity for deduplication, retained if the source recipe is deleted.</summary>
    public Guid? SharedFromRecipeId { get; set; }

    public RecipeTitle Title { get; set; }

    public string? Description { get; set; }
    public string? SourceUrl { get; set; }

    /// <summary>Seeder identity, independent of user-editable source links.</summary>
    public string? CollectionSeedKey { get; set; }

    /// <summary>
    /// Preparation time in minutes (voorbereiden).
    /// </summary>
    public int PreparationMinutes { get; set; }

    /// <summary>
    /// Cooking time in minutes (bereiden).
    /// </summary>
    public int CookingMinutes { get; set; }

    public RecipeCategory Category { get; set; }

    public string? ImageUrl { get; set; }

    public Difficulty Difficulty { get; set; }

    public int Servings { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Latest content-moderation outcome for this recipe.</summary>
    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.NotModerated;

    public DateTimeOffset? ModeratedAt { get; set; }

    /// <summary>Compact category/severity summary from the last automated check.</summary>
    public string? ModerationSummary { get; set; }

    public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

    public ICollection<InstructionStep> InstructionSteps { get; set; } = new List<InstructionStep>();

    public ICollection<RecipeRating> Ratings { get; } = new List<RecipeRating>();

    public Recipe CopyFor(string ownerUserId)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new Recipe
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Title = Title,
            Description = Description,
            SourceUrl = SourceUrl,
            ImageUrl = ImageUrl,
            PreparationMinutes = PreparationMinutes,
            CookingMinutes = CookingMinutes,
            Category = Category,
            Difficulty = Difficulty,
            Servings = Servings,
            CreatedAt = now,
            UpdatedAt = now,
            ModerationStatus = ModerationStatus,
            ModeratedAt = ModeratedAt,
            ModerationSummary = ModerationSummary,
            Ingredients = Ingredients.Select(ingredient => new Ingredient
            {
                Id = Guid.NewGuid(),
                RecipeId = id,
                IngredientId = ingredient.IngredientId,
                Name = ingredient.Name,
                Preparation = ingredient.Preparation,
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
            }).ToList(),
            InstructionSteps = InstructionSteps.Select(step => new InstructionStep
            {
                Id = Guid.NewGuid(),
                RecipeId = id,
                StepNumber = step.StepNumber,
                Text = step.Text,
            }).ToList(),
        };
    }
}
