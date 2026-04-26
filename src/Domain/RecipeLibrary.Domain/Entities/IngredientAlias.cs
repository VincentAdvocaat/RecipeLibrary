namespace RecipeLibrary.Domain.Entities;

public sealed class IngredientAlias
{
    public Guid Id { get; set; }

    public Guid IngredientId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string NormalizedAlias { get; set; } = string.Empty;

    public CanonicalIngredient Ingredient { get; set; } = null!;
}
