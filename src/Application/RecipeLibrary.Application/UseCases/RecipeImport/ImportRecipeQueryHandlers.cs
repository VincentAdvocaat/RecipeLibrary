using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.RecipeImport;

namespace RecipeLibrary.Application.UseCases.RecipeImport;

public sealed class ImportRecipeContentQueryHandler(
    RecipeImportService recipeImportService)
    : IQueryHandler<ImportRecipeContentQuery, ImportRecipeResult>
{
    public Task<ImportRecipeResult> HandleAsync(ImportRecipeContentQuery query, CancellationToken ct = default) =>
        recipeImportService.ImportContentAsync(query, ct);
}

public sealed class ImportRecipeFromUrlQueryHandler(
    IRecipeImportContentFetcher contentFetcher,
    RecipeImportService recipeImportService)
    : IQueryHandler<ImportRecipeFromUrlQuery, ImportRecipeResult>
{
    public async Task<ImportRecipeResult> HandleAsync(ImportRecipeFromUrlQuery query, CancellationToken ct = default)
    {
        var url = (query.Url ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            throw new ArgumentException("URL is required.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("URL must be an absolute http or https address.");
        }

        var html = await contentFetcher.FetchHtmlAsync(url, ct);
        return await recipeImportService.ImportContentAsync(
            new ImportRecipeContentQuery
            {
                Content = html,
                ContentKind = ImportContentKind.Html,
            },
            ct);
    }
}
