namespace RecipeLibrary.Domain.Entities;

public sealed class IngredientTag
{
    public Guid IngredientId { get; set; }

    public Guid TagId { get; set; }

    public CanonicalIngredient Ingredient { get; set; } = null!;

    public Tag Tag { get; set; } = null!;
}
