namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRatingRepository
{
    Task<bool> IsRecipeAccessibleAsync(string userId, Guid recipeId, CancellationToken ct = default);

    /// <summary>Saves one user's rating. Zero clears the rating.</summary>
    Task SetRatingAsync(string userId, Guid recipeId, int stars, CancellationToken ct = default);
}
