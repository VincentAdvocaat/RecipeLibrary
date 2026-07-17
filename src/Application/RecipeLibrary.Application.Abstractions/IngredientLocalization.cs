using System.Globalization;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Builds the BCP-47 language fallback chain used for matching and display resolution.
/// Order: exact culture → CultureInfo.Parent chain → terminal "en".
/// </summary>
public static class IngredientLanguageFallback
{
    public const string TerminalLanguage = "en";

    public static IReadOnlyList<string> ResolveChain(string? cultureName)
    {
        var chain = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? code)
        {
            var normalized = NormalizeLanguageCode(code);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                return;
            }

            chain.Add(normalized);
        }

        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName.Trim());
                while (!Equals(culture, CultureInfo.InvariantCulture)
                       && !string.IsNullOrEmpty(culture.Name))
                {
                    Add(culture.Name);
                    if (Equals(culture.Parent, culture))
                    {
                        break;
                    }

                    culture = culture.Parent;
                }
            }
            catch (CultureNotFoundException)
            {
                Add(cultureName);
            }
        }

        Add(TerminalLanguage);
        return chain;
    }

    public static string NormalizeLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return code.Trim().Replace("_", "-", StringComparison.Ordinal);
    }
}

/// <summary>
/// Resolves a display name for a canonical ingredient using the language fallback chain.
/// </summary>
public static class IngredientDisplayResolver
{
    public static IngredientDisplay Resolve(
        CanonicalIngredient ingredient,
        IReadOnlyList<string> languageChain)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        ArgumentNullException.ThrowIfNull(languageChain);

        foreach (var language in languageChain)
        {
            var translation = FindTranslation(ingredient, language);
            if (translation is not null)
            {
                return new IngredientDisplay(translation.DisplayName, translation.LanguageCode);
            }
        }

        var english = FindTranslation(ingredient, IngredientLanguageFallback.TerminalLanguage);
        if (english is not null)
        {
            return new IngredientDisplay(english.DisplayName, english.LanguageCode);
        }

        var any = ingredient.Translations.FirstOrDefault();
        if (any is not null)
        {
            return new IngredientDisplay(any.DisplayName, any.LanguageCode);
        }

        if (!string.IsNullOrWhiteSpace(ingredient.CatalogKey))
        {
            return new IngredientDisplay(ingredient.CatalogKey, LanguageCode: null);
        }

        return new IngredientDisplay(string.Empty, LanguageCode: null);
    }

    public static string? ResolveNormalizedDisplayName(
        CanonicalIngredient ingredient,
        IReadOnlyList<string> languageChain)
    {
        foreach (var language in languageChain)
        {
            var translation = FindTranslation(ingredient, language);
            if (translation is not null)
            {
                return translation.NormalizedDisplayName;
            }
        }

        return ingredient.Translations.FirstOrDefault()?.NormalizedDisplayName;
    }

    private static IngredientTranslation? FindTranslation(CanonicalIngredient ingredient, string language) =>
        ingredient.Translations.FirstOrDefault(x =>
            string.Equals(x.LanguageCode, language, StringComparison.OrdinalIgnoreCase));
}

public readonly record struct IngredientDisplay(string DisplayName, string? LanguageCode);
