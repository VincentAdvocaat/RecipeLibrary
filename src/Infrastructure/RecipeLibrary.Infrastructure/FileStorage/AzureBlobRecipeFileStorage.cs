using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class AzureBlobRecipeFileStorage : IRecipeFileStorage
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

    private readonly BlobContainerClient _containerClient;

    public AzureBlobRecipeFileStorage(IOptions<RecipeFileStorageOptions> options)
    {
        var azure = options.Value.AzureBlob;
        var accountName = (azure.AccountName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(accountName))
        {
            throw new InvalidOperationException(
                "Azure Blob recipe image storage requires RecipeFileStorage:AzureBlob:AccountName.");
        }

        var containerName = (azure.ContainerName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(containerName))
        {
            throw new InvalidOperationException(
                "Azure Blob recipe image storage requires RecipeFileStorage:AzureBlob:ContainerName.");
        }

        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        var serviceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        _containerClient = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(suggestedFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Invalid image extension. Use jpg, png, gif or webp.", nameof(suggestedFileName));
        }

        var storageKey = $"{Guid.NewGuid()}{ext}";
        var blobClient = _containerClient.GetBlobClient(storageKey);
        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);
        return $"/api/recipe-images/{storageKey}";
    }

    public async Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.IndexOfAny(['/', '\\']) >= 0)
        {
            return null;
        }

        var ext = Path.GetExtension(storageKey).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext) ||
            !ExtensionToContentType.TryGetValue(ext, out var contentType))
        {
            return null;
        }

        var blobClient = _containerClient.GetBlobClient(storageKey);
        if (!await blobClient.ExistsAsync(ct))
        {
            return null;
        }

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return (response.Value.Content, contentType);
    }
}
