namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Validates that a recipe import URL is a public HTTP(S) endpoint (SSRF protection).
/// </summary>
public interface IRecipeImportUrlGuard
{
    Task EnsurePublicHttpUrlAsync(string url, CancellationToken ct = default);
}
