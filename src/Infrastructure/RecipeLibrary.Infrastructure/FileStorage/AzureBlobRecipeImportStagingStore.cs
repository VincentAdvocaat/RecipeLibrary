using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class AzureBlobRecipeImportStagingStore : IRecipeImportStagingStore
{
    private const int MaxOptimisticRetries = 5;
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

        await WriteSessionAsync(session, ifMatch: null, ct);
        return session;
    }

    public async Task<RecipeImportStagingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var loaded = await TryLoadSessionAsync(sessionId, ct);
        return loaded?.Session;
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
        // Buffer once so optimistic retries can re-upload after a conflicting session write.
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);
        var payload = buffered.ToArray();

        for (var attempt = 0; attempt < MaxOptimisticRetries; attempt++)
        {
            var loaded = await TryLoadSessionAsync(sessionId, ct)
                ?? throw new InvalidOperationException("Import session was not found or has expired.");
            EnsureActiveOwner(loaded.Session, ownerKey);

            if (loaded.Session.Images.Count >= maxImages)
            {
                throw new ArgumentException($"A maximum of {maxImages} images is allowed per import session.");
            }

            var ext = NormalizeExtension(fileName, contentType);
            var imageId = Guid.NewGuid().ToString("N");
            var order = loaded.Session.Images.Count == 0 ? 1 : loaded.Session.Images.Max(x => x.Order) + 1;
            var blobName = ImageBlobKey(sessionId, order, imageId, ext);
            var blob = _container.GetBlobClient(blobName);
            await using (var uploadStream = new MemoryStream(payload, writable: false))
            {
                await blob.UploadAsync(
                    uploadStream,
                    new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                    ct);
            }

            var image = new RecipeImportStagingImage
            {
                ImageId = imageId,
                Order = order,
                FileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(blobName) : fileName.Trim(),
                ContentType = contentType,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            loaded.Session.Images.Add(image);

            try
            {
                await WriteSessionAsync(loaded.Session, loaded.ETag, ct);
                return image;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
            }
        }

        throw new InvalidOperationException("Could not update the import session due to concurrent changes. Please retry.");
    }

    public async Task RemoveImageAsync(string sessionId, string ownerKey, string imageId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxOptimisticRetries; attempt++)
        {
            var loaded = await TryLoadSessionAsync(sessionId, ct)
                ?? throw new InvalidOperationException("Import session was not found or has expired.");
            EnsureActiveOwner(loaded.Session, ownerKey);

            var image = loaded.Session.Images.FirstOrDefault(x => string.Equals(x.ImageId, imageId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Staged image was not found.");

            loaded.Session.Images.Remove(image);

            try
            {
                await WriteSessionAsync(loaded.Session, loaded.ETag, ct);
                await DeleteImageBlobsAsync(sessionId, image.ImageId, ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // Retry with the latest session marker.
            }
        }

        throw new InvalidOperationException("Could not update the import session due to concurrent changes. Please retry.");
    }

    public async Task<(Stream Stream, string ContentType)?> OpenImageAsync(
        string sessionId,
        string ownerKey,
        string imageId,
        CancellationToken ct = default)
    {
        var loaded = await TryLoadSessionAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Import session was not found or has expired.");
        EnsureActiveOwner(loaded.Session, ownerKey);

        var image = loaded.Session.Images.FirstOrDefault(x => string.Equals(x.ImageId, imageId, StringComparison.OrdinalIgnoreCase));
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

    private async Task<(RecipeImportStagingSession Session, ETag ETag)?> TryLoadSessionAsync(
        string sessionId,
        CancellationToken ct)
    {
        var blob = _container.GetBlobClient(SessionMarkerKey(sessionId));
        try
        {
            var response = await blob.DownloadContentAsync(ct);
            var session = response.Value.Content.ToObjectFromJson<RecipeImportStagingSession>(JsonOptions);
            if (session is null)
            {
                return null;
            }

            return (session, response.Value.Details.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static void EnsureActiveOwner(RecipeImportStagingSession session, string ownerKey)
    {
        if (!string.IsNullOrEmpty(session.OwnerKey)
            && !string.Equals(session.OwnerKey, ownerKey ?? string.Empty, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Import session does not belong to the current user.");
        }

        if (session.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Import session has expired.");
        }
    }

    private async Task WriteSessionAsync(RecipeImportStagingSession session, ETag? ifMatch, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(SessionMarkerKey(session.SessionId));
        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, ct);
        stream.Position = 0;

        var options = new BlobUploadOptions();
        if (ifMatch is { } etag)
        {
            options.Conditions = new BlobRequestConditions { IfMatch = etag };
        }

        await blob.UploadAsync(stream, options, ct);
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
