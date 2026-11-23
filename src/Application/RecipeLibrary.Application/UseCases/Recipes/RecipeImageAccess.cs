using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.UseCases.Recipes;

internal static class RecipeImageAccess
{
    public static async Task EnsureCanAttachAsync(
        IRecipeRepository recipes, string userId, string imageUrl, CancellationToken ct)
    {
        if (GetStoredImageKey(imageUrl) is { } imageKey
            && !await recipes.IsRecipeImageAccessibleAsync(userId, imageKey, ct))
        {
            throw new ArgumentException("The recipe image is unavailable.", nameof(imageUrl));
        }
    }

    internal static string? GetStoredImageKey(string? imageUrl)
    {
        if (imageUrl is null)
        {
            return null;
        }

        const string legacyPrefix = "/api/recipe-images/";
        const string versionedPrefix = "/api/v1/recipe-images/";
        var prefix = imageUrl.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase) ? legacyPrefix
            : imageUrl.StartsWith(versionedPrefix, StringComparison.OrdinalIgnoreCase) ? versionedPrefix : null;
        if (prefix is null)
        {
            return null;
        }

        var key = Uri.UnescapeDataString(imageUrl[prefix.Length..].Split('?', '#')[0]);
        return key.Length > 0 && !key.Contains("..", StringComparison.Ordinal)
            && key.IndexOfAny(['/', '\\']) < 0 && !key.Any(char.IsControl) ? key : null;
    }
}
