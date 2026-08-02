using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a recipe with its ingredients and instruction steps.
/// Product decision (E14 / E16.F2.T7): each recipe belongs to exactly one Identity user
/// (<see cref="OwnerUserId"/>). Libraries are private per user — no shared recipe edit.
/// </summary>
public sealed class Recipe
{
    public Guid Id { get; set; }

    /// <summary>ASP.NET Core Identity user id of the recipe owner (private library).</summary>
    public string OwnerUserId { get; set; } = string.Empty;

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
}

