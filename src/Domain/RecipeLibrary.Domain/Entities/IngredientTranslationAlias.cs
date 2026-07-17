namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Language-specific synonym for an <see cref="IngredientTranslation"/>.
/// </summary>
public sealed class IngredientTranslationAlias
{
    public Guid Id { get; set; }

    public Guid IngredientTranslationId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string NormalizedAlias { get; set; } = string.Empty;

    public IngredientTranslation Translation { get; set; } = null!;
}
