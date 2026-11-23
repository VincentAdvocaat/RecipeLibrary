using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeShareRepository
{
    Task<Guid?> GetLinkKeyAsync(string ownerUserId, Guid recipeId, CancellationToken ct);
    Task<Guid?> SetLinkAsync(string ownerUserId, Guid recipeId, bool enabled, CancellationToken ct);
    Task<Recipe?> GetSharedRecipeAsync(Guid key, CancellationToken ct);
    Task<string?> GetSharedImageUrlAsync(Guid key, CancellationToken ct);
    Task<Guid?> FindSavedCopyAsync(string ownerUserId, Guid sourceId, CancellationToken ct);
}
