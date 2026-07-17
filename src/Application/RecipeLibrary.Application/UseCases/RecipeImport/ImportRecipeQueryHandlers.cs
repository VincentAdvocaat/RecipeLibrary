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
    IRecipeSocialCaptionFetcher socialCaptionFetcher,
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

        await RecipeImportUrlSafety.EnsurePublicHttpUrlAsync(url, ct);

        // Instagram/YouTube pages are JS shells; captions live in platform APIs.
        var socialCaption = await socialCaptionFetcher.TryFetchCaptionAsync(url, ct);
        if (!string.IsNullOrWhiteSpace(socialCaption))
        {
            return await recipeImportService.ImportPlainTextAsync(socialCaption, query.ParseOptions, ct);
        }

        var html = await contentFetcher.FetchHtmlAsync(url, ct);
        var text = recipeImportService.HtmlToRecipeText(html);
        return await recipeImportService.ImportPlainTextAsync(text, query.ParseOptions, ct);
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
        var images = ResolveImages(query);
        if (images.Count == 0)
        {
            throw new ArgumentException("Image is required.");
        }

        var maxImages = Math.Max(1, options.Value.Ocr.MaxImagesPerImport);
        if (images.Count > maxImages)
        {
            throw new ArgumentException($"At most {maxImages} images are allowed per import.");
        }

        var maxBytes = options.Value.Ocr.MaxImageBytes;
        var language = NormalizeLanguage(query.Language);
        var textParts = new List<string>(images.Count);

        foreach (var image in images)
        {
            if (image.ImageBytes is null || image.ImageBytes.Length == 0)
            {
                throw new ArgumentException("Image is required.");
            }

            if (image.ImageBytes.Length > maxBytes)
            {
                throw new ArgumentException($"Image exceeds maximum size of {maxBytes} bytes.");
            }

            ResolveContentType(image.ContentType, image.FileName);

            await using var stream = new MemoryStream(image.ImageBytes, writable: false);
            var text = await imageTextExtractor.ExtractTextAsync(stream, language, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                textParts.Add(text.Trim());
            }
        }

        if (textParts.Count == 0)
        {
            throw new InvalidOperationException("No text could be extracted from the image.");
        }

        var combined = textParts.Count == 1
            ? textParts[0]
            : string.Join("\n\n", textParts);

        return await recipeImportService.ImportPlainTextAsync(combined, query.ParseOptions, ct);
    }

    private static IReadOnlyList<ImportImageFile> ResolveImages(ImportRecipeFromImageQuery query)
    {
        if (query.Images is { Count: > 0 })
        {
            return query.Images;
        }

        if (query.ImageBytes is { Length: > 0 })
        {
            return
            [
                new ImportImageFile
                {
                    ImageBytes = query.ImageBytes,
                    ContentType = query.ContentType,
                    FileName = query.FileName,
                },
            ];
        }

        return [];
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
