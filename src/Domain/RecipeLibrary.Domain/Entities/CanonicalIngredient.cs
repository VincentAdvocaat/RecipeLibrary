namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Canonical ingredient entity used for normalization and matching.
/// </summary>
public sealed class CanonicalIngredient
{
    public Guid Id { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<IngredientAlias> Aliases { get; set; } = new List<IngredientAlias>();

    public ICollection<IngredientTag> IngredientTags { get; set; } = new List<IngredientTag>();
}
