using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Ingredient belonging to a recipe.
/// </summary>
public sealed class Ingredient
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public Guid? IngredientId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Preparation { get; set; }

    /// <summary>Null when the ingredient is unmeasured (e.g. naar smaak).</summary>
    public Quantity? Quantity { get; set; }

    /// <summary>Null when the ingredient is unmeasured (e.g. naar smaak).</summary>
    public Unit? Unit { get; set; }

    public CanonicalIngredient? IngredientDefinition { get; set; }
}
