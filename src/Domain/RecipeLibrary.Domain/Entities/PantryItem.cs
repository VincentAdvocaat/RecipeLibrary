using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

public sealed class PantryItem
{
    public Guid Id { get; set; }

    /// <summary>Entra object ID or anonymous group key (group:{guid}).</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    public Guid? CanonicalIngredientId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public Quantity Quantity { get; set; }

    public Unit Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CanonicalIngredient? Ingredient { get; set; }
}
