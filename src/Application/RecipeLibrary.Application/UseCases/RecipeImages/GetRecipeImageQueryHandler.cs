using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.RecipeImages;

public sealed class GetRecipeImageQueryHandler(IRecipeFileStorage storage)
    : IQueryHandler<GetRecipeImageQuery, GetRecipeImageResult?>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public async Task<GetRecipeImageResult?> HandleAsync(GetRecipeImageQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var key = (query.StorageKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key) || key.Contains("..", StringComparison.Ordinal) ||
            key.IndexOfAny(['/', '\\']) >= 0)
        {
            return null;
        }

        var ext = Path.GetExtension(key).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return null;
        }

        var result = await storage.OpenAsync(key, ct);
        return result.HasValue ? new GetRecipeImageResult(result.Value.Stream, result.Value.ContentType) : null;
    }
}
