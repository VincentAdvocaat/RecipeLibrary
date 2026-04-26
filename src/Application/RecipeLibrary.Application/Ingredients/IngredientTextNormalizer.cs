using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.Ingredients;

public sealed partial class IngredientTextNormalizer : IIngredientTextNormalizer
{
    public string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = input.Trim().ToLowerInvariant();
        value = MultipleWhitespaceRegex().Replace(value, " ");
        value = RemoveDiacritics(value);
        return ApplySimpleTypoCleanup(value);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ApplySimpleTypoCleanup(string value)
    {
        var cleaned = value;
        cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        cleaned = cleaned.Replace(" gembre", " gember", StringComparison.Ordinal);
        return cleaned.Trim();
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex MultipleWhitespaceRegex();
}
