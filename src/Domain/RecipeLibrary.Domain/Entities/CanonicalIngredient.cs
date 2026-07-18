namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Language-neutral canonical ingredient used for identity, linking, and matching.
/// Display names live on <see cref="IngredientTranslation"/>.
/// </summary>
public sealed class CanonicalIngredient
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable catalog key from curated seed data (e.g. Open Food Facts-derived id).
    /// </summary>
    public string? CatalogKey { get; set; }

    /// <summary>
    /// True when this ingredient was created from user input rather than curated catalog seed data.
    /// </summary>
    public bool UserGenerated { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<IngredientTranslation> Translations { get; set; } = new List<IngredientTranslation>();

    public ICollection<IngredientTag> IngredientTags { get; set; } = new List<IngredientTag>();
}
