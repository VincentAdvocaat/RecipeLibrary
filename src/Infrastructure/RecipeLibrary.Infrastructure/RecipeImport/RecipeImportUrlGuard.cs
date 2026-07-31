using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public sealed class RecipeImportUrlGuard : IRecipeImportUrlGuard
{
    public Task EnsurePublicHttpUrlAsync(string url, CancellationToken ct = default) =>
        RecipeImportUrlSafety.EnsurePublicHttpUrlAsync(url, ct);
}
