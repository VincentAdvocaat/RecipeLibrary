using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.UseCases.Recipes;

internal static class RecipeImageAccess
{
    public static async Task EnsureCanAttachAsync(
        IRecipeRepository recipes, string userId, string imageUrl, CancellationToken ct)
    {
        if (imageUrl.StartsWith("/api/recipe-images/", StringComparison.OrdinalIgnoreCase))
        {
            var imageKey = Uri.UnescapeDataString(Path.GetFileName(imageUrl));
            if (!await recipes.IsRecipeImageAccessibleAsync(userId, imageKey, ct))
            {
                throw new ArgumentException("The recipe image is unavailable.", nameof(imageUrl));
            }
        }
    }
}
