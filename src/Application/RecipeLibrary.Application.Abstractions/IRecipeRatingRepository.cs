namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRatingRepository
{
    /// <summary>Saves one user's rating if they have access. Zero clears the rating.</summary>
    Task<bool> TrySetRatingAsync(string userId, Guid recipeId, int stars, CancellationToken ct = default);
}
