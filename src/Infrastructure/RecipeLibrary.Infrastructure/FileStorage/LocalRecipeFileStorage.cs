using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class LocalRecipeFileStorage : IRecipeFileStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    private readonly string _basePath;

    public LocalRecipeFileStorage(IOptions<LocalRecipeFileStorageOptions> options)
    {
        var path = (options?.Value?.BasePath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException(
                "LocalRecipeFileStorage requires RecipeFileStorage:LocalBasePath or a default base path when registering.");
        }
        _basePath = Path.GetFullPath(path);
    }

    public async Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(suggestedFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Invalid image extension. Use jpg, png, gif or webp.", nameof(suggestedFileName));
        }

        var recipeImagesDir = Path.Combine(_basePath, "recipe-images");
        Directory.CreateDirectory(recipeImagesDir);

        var storageKey = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(recipeImagesDir, storageKey);

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
        }

        return $"/api/recipe-images/{storageKey}";
    }

    public Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.IndexOfAny(['/', '\\']) >= 0)
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var ext = Path.GetExtension(storageKey).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext) ||
            !ExtensionToContentType.TryGetValue(ext, out var contentType))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var filePath = Path.Combine(_basePath, "recipe-images", storageKey);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<(Stream Stream, string ContentType)?>((stream, contentType));
    }
}
