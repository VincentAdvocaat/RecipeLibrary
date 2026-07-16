using Microsoft.Extensions.Options;
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

public sealed class ImportRecipeFromImageQueryHandler(
    IRecipeImageTextExtractor imageTextExtractor,
    RecipeImportService recipeImportService,
    IOptions<RecipeImportOptions> options)
    : IQueryHandler<ImportRecipeFromImageQuery, ImportRecipeResult>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
    };

    public async Task<ImportRecipeResult> HandleAsync(ImportRecipeFromImageQuery query, CancellationToken ct = default)
    {
        if (query.ImageBytes is null || query.ImageBytes.Length == 0)
        {
            throw new ArgumentException("Image is required.");
        }

        var maxBytes = options.Value.Ocr.MaxImageBytes;
        if (query.ImageBytes.Length > maxBytes)
        {
            throw new ArgumentException($"Image exceeds maximum size of {maxBytes} bytes.");
        }

        ResolveContentType(query.ContentType, query.FileName);

        var language = NormalizeLanguage(query.Language);
        await using var stream = new MemoryStream(query.ImageBytes, writable: false);
        var text = await imageTextExtractor.ExtractTextAsync(stream, language, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("No text could be extracted from the image.");
        }

        return await recipeImportService.ImportContentAsync(
            new ImportRecipeContentQuery
            {
                Content = text,
                ContentKind = ImportContentKind.PlainText,
            },
            ct);
    }

    internal static string ResolveContentType(string? contentType, string? fileName)
    {
        var type = (contentType ?? string.Empty).Trim();
        if (AllowedContentTypes.Contains(type))
        {
            return NormalizeJpegAlias(type);
        }

        if (type.Length > 0
            && !string.Equals(type, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unsupported image type. Use jpg, png, or webp.");
        }

        var inferred = InferContentTypeFromFileName(fileName);
        if (inferred is null)
        {
            throw new ArgumentException("Unsupported image type. Use jpg, png, or webp.");
        }

        return inferred;
    }

    private static string? InferContentTypeFromFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(name).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null,
        };
    }

    private static string NormalizeJpegAlias(string contentType) =>
        string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : contentType;

    private static string NormalizeLanguage(string? language)
    {
        var value = (language ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "en" or "eng" or "english" => "eng",
            "nl" or "nld" or "dutch" or "nederlands" => "nld",
            _ => "nld",
        };
    }
}
