using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

internal static class IngredientTestFactory
{
    public static CanonicalIngredient Create(
        string displayName,
        string? languageCode = "nl",
        Guid? id = null,
        string? catalogKey = null,
        params string[] aliases)
    {
        var ingredientId = id ?? Guid.NewGuid();
        var normalized = displayName.Trim().ToLowerInvariant();
        var translation = new IngredientTranslation
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredientId,
            LanguageCode = languageCode ?? "nl",
            DisplayName = displayName,
            NormalizedDisplayName = normalized,
            Aliases = aliases.Select(a => new IngredientTranslationAlias
            {
                Id = Guid.NewGuid(),
                Alias = a,
                NormalizedAlias = a.Trim().ToLowerInvariant(),
            }).ToList(),
        };

        return new CanonicalIngredient
        {
            Id = ingredientId,
            CatalogKey = catalogKey,
            CreatedAt = DateTimeOffset.UtcNow,
            Translations = [translation],
        };
    }
}
