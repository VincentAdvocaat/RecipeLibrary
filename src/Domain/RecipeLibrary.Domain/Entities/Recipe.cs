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

    /// <summary>Source identity for deduplication, retained if the personal recipe is deleted.</summary>
    public Guid? SharedFromRecipeId { get; set; }

    public RecipeTitle Title { get; set; }

    public string? Description { get; set; }

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
}

