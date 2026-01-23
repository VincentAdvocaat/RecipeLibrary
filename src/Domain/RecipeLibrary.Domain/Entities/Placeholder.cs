namespace RecipeLibrary.Domain.Entities;

using RecipeLibrary.Domain.ValueObjects;

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

/// <summary>
/// Ingredient belonging to a recipe.
/// </summary>
public sealed class Ingredient
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Quantity Quantity { get; set; }

    public Unit Unit { get; set; }
}

/// <summary>
/// One step in the preparation of a recipe.
/// </summary>
public sealed class InstructionStep
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public int StepNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}


