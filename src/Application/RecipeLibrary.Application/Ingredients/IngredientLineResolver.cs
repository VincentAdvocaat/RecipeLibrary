namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Resolves display name and preparation from user input (name field and optional explicit preparation).
/// </summary>
public sealed class IngredientLineResolver(IngredientNameParser parser)
{
    public ResolvedIngredientLine Resolve(string? name, string? preparation)
    {
        var rawName = (name ?? string.Empty).Trim();
        var explicitPreparation = string.IsNullOrWhiteSpace(preparation) ? null : preparation.Trim();

        if (rawName.Length == 0 && explicitPreparation is null)
        {
            return new ResolvedIngredientLine(string.Empty, null);
        }

        if (explicitPreparation is not null)
        {
            var stripped = parser.ParseIngredient(rawName);
            var displayName = stripped.Name.Length > 0 ? stripped.Name : rawName;
            return new ResolvedIngredientLine(displayName, explicitPreparation);
        }

        var parsed = parser.ParseIngredient(rawName);
        return new ResolvedIngredientLine(parsed.Name, parsed.Preparation);
    }
}

public sealed record ResolvedIngredientLine(string DisplayName, string? Preparation);
