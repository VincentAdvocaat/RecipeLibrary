using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class AzureBlobRecipeImportStagingStore : IRecipeImportStagingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private readonly BlobContainerClient _container;

    public AzureBlobRecipeImportStagingStore(IOptions<RecipeFileStorageOptions> options)
    {
        var azure = options.Value.AzureBlob;
        var accountName = (azure.AccountName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(accountName))
        {
            throw new InvalidOperationException(
                "Azure Blob staging requires RecipeFileStorage:AzureBlob:AccountName.");
        }

        var containerName = (azure.StagingContainerName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(containerName))
        {
            throw new InvalidOperationException(
                "Azure Blob staging requires RecipeFileStorage:AzureBlob:StagingContainerName.");
        }

        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        var serviceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<RecipeImportStagingSession> CreateSessionAsync(
        string ownerKey,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new RecipeImportStagingSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            OwnerKey = ownerKey ?? string.Empty,
            CreatedUtc = now,
            ExpiresUtc = now.Add(ttl),
            Images = [],
        };

        await WriteSessionAsync(session, ct);
        return session;
    }

    public async Task<RecipeImportStagingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(SessionMarkerKey(sessionId));
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        await using var stream = await blob.OpenReadAsync(cancellationToken: ct);
        return await JsonSerializer.DeserializeAsync<RecipeImportStagingSession>(stream, JsonOptions, ct);
    }

    public async Task<RecipeImportStagingImage> AddImageAsync(
        string sessionId,
        string ownerKey,
        Stream content,
        string fileName,
        string contentType,
        int maxImages,
        CancellationToken ct = default)
    {
        var session = await RequireActiveSessionAsync(sessionId, ownerKey, ct);
        if (session.Images.Count >= maxImages)
        {
            throw new ArgumentException($"A maximum of {maxImages} images is allowed per import session.");
        }

        var ext = NormalizeExtension(fileName, contentType);
        var imageId = Guid.NewGuid().ToString("N");
        var order = session.Images.Count == 0 ? 1 : session.Images.Max(x => x.Order) + 1;
        var blobName = ImageBlobKey(sessionId, order, imageId, ext);
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);

        var image = new RecipeImportStagingImage
        {
            ImageId = imageId,
            Order = order,
            FileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(blobName) : fileName.Trim(),
            ContentType = contentType,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        session.Images.Add(image);
        await WriteSessionAsync(session, ct);
        return image;
    }

    public async Task RemoveImageAsync(string sessionId, string ownerKey, string imageId, CancellationToken ct = default)
    {
        var session = await RequireActiveSessionAsync(sessionId, ownerKey, ct);
        var image = session.Images.FirstOrDefault(x => string.Equals(x.ImageId, imageId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Staged image was not found.");

        session.Images.Remove(image);
        await DeleteImageBlobsAsync(sessionId, image.ImageId, ct);
        await WriteSessionAsync(session, ct);
    }

    public async Task<(Stream Stream, string ContentType)?> OpenImageAsync(
        string sessionId,
        string ownerKey,
        string imageId,
        CancellationToken ct = default)
    {
        var session = await RequireActiveSessionAsync(sessionId, ownerKey, ct);
        var image = session.Images.FirstOrDefault(x => string.Equals(x.ImageId, imageId, StringComparison.OrdinalIgnoreCase));
        if (image is null)
        {
            return null;
        }

        await foreach (var item in _container.GetBlobsAsync(prefix: $"{Sanitize(sessionId)}/", cancellationToken: ct))
        {
            if (!item.Name.Contains(image.ImageId, StringComparison.OrdinalIgnoreCase)
                || item.Name.EndsWith("_session.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var blob = _container.GetBlobClient(item.Name);
            var stream = await blob.OpenReadAsync(cancellationToken: ct);
            return (stream, image.ContentType);
        }

        return null;
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var prefix = $"{Sanitize(sessionId)}/";
        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            await _container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: ct);
        }
    }

    public async Task<int> DeleteExpiredSessionsAsync(CancellationToken ct = default)
    {
        var removed = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var item in _container.GetBlobsAsync(traits: BlobTraits.None, cancellationToken: ct))
        {
            if (!item.Name.EndsWith("/_session.json", StringComparison.OrdinalIgnoreCase)
                && !item.Name.EndsWith("_session.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sessionId = item.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(sessionId) || !seen.Add(sessionId))
            {
                continue;
            }

            var session = await GetSessionAsync(sessionId, ct);
            if (session is null || session.ExpiresUtc > DateTimeOffset.UtcNow)
            {
                continue;
            }

            await DeleteSessionAsync(sessionId, ct);
            removed++;
        }

        return removed;
    }

    private async Task<RecipeImportStagingSession> RequireActiveSessionAsync(
        string sessionId,
        string ownerKey,
        CancellationToken ct)
    {
        var session = await GetSessionAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Import session was not found or has expired.");

        if (!string.IsNullOrEmpty(session.OwnerKey)
            && !string.Equals(session.OwnerKey, ownerKey ?? string.Empty, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Import session does not belong to the current user.");
        }

        if (session.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Import session has expired.");
        }

        return session;
    }

    private async Task WriteSessionAsync(RecipeImportStagingSession session, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(SessionMarkerKey(session.SessionId));
        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, ct);
        stream.Position = 0;
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
    }

    private async Task DeleteImageBlobsAsync(string sessionId, string imageId, CancellationToken ct)
    {
        await foreach (var item in _container.GetBlobsAsync(prefix: $"{Sanitize(sessionId)}/", cancellationToken: ct))
        {
            if (item.Name.Contains(imageId, StringComparison.OrdinalIgnoreCase))
            {
                await _container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: ct);
            }
        }
    }

    private static string SessionMarkerKey(string sessionId) => $"{Sanitize(sessionId)}/_session.json";

    private static string ImageBlobKey(string sessionId, int order, string imageId, string ext) =>
        $"{Sanitize(sessionId)}/{order:D2}_{imageId}{ext}";

    private static string Sanitize(string sessionId)
    {
        var value = (sessionId ?? string.Empty).Trim();
        if (value.Length == 0 || value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("Invalid session id.");
        }

        return value;
    }

    private static string NormalizeExtension(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (AllowedExtensions.Contains(ext))
        {
            return ext == ".jpeg" ? ".jpg" : ext;
        }

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg",
        };
    }
}
