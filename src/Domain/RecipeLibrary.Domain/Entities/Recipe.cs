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
    /// Total preparation and cooking duration.
    /// </summary>
    public Duration Duration { get; set; }

    public Difficulty Difficulty { get; set; }

    public int Servings { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

    public ICollection<InstructionStep> InstructionSteps { get; set; } = new List<InstructionStep>();
}

