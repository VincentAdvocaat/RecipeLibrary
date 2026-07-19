namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Staple the owner already keeps at home (presence only; no inventory quantity).
/// </summary>
public sealed class PantryItem
{
    public Guid Id { get; set; }

    /// <summary>Identity user id or anonymous group key (group:{guid}).</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    public Guid? CanonicalIngredientId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CanonicalIngredient? Ingredient { get; set; }
}
