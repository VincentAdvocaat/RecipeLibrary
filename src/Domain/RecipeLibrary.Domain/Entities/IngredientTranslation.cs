namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Preferred display name for a canonical ingredient in a single BCP-47 language.
/// </summary>
public sealed class IngredientTranslation
{
    public Guid Id { get; set; }

    public Guid IngredientId { get; set; }

    /// <summary>
    /// BCP-47 language/culture tag (e.g. nl, en, en-US, pt-BR).
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string NormalizedDisplayName { get; set; } = string.Empty;

    public CanonicalIngredient Ingredient { get; set; } = null!;

    public ICollection<IngredientTranslationAlias> Aliases { get; set; } = new List<IngredientTranslationAlias>();
}
