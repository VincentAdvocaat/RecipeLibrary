using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a recipe with its ingredients and instruction steps.
/// </summary>
public sealed class Recipe
{
    public Guid Id { get; set; }

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

    public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

    public ICollection<InstructionStep> InstructionSteps { get; set; } = new List<InstructionStep>();
}

