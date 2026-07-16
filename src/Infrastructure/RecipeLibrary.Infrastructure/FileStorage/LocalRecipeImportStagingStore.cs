using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class LocalRecipeImportStagingStore(IOptions<RecipeFileStorageOptions> options)
    : IRecipeImportStagingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _root = ResolveRoot(options.Value);

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

        var dir = GetSessionDir(session.SessionId);
        Directory.CreateDirectory(dir);
        await WriteSessionAsync(session, ct);
        return session;
    }

    public async Task<RecipeImportStagingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var path = GetSessionMarkerPath(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RecipeImportStagingSession>(stream, JsonOptions, ct);
    }

    public Task<RecipeImportStagingImage> AddImageAsync(
        string sessionId,
        string ownerKey,
        Stream content,
        string fileName,
        string contentType,
        int maxImages,
        CancellationToken ct = default) =>
        WithSessionLockAsync(sessionId, async () =>
        {
            var session = await RequireActiveSessionAsync(sessionId, ownerKey, ct);
            if (session.Images.Count >= maxImages)
            {
                throw new ArgumentException($"A maximum of {maxImages} images is allowed per import session.");
            }

            var ext = NormalizeExtension(fileName, contentType);
            var imageId = Guid.NewGuid().ToString("N");
            var order = session.Images.Count == 0 ? 1 : session.Images.Max(x => x.Order) + 1;
            var storageName = $"{order:D2}_{imageId}{ext}";
            var imagePath = Path.Combine(GetSessionDir(sessionId), storageName);

            await using (var file = File.Create(imagePath))
            {
                await content.CopyToAsync(file, ct);
            }

            var image = new RecipeImportStagingImage
            {
                ImageId = imageId,
                Order = order,
                FileName = string.IsNullOrWhiteSpace(fileName) ? storageName : fileName.Trim(),
                ContentType = contentType,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            session.Images.Add(image);
            await WriteSessionAsync(session, ct);
            return image;
        }, ct);

    public Task RemoveImageAsync(string sessionId, string ownerKey, string imageId, CancellationToken ct = default) =>
        WithSessionLockAsync(sessionId, async () =>
        {
            var session = await RequireActiveSessionAsync(sessionId, ownerKey, ct);
            var image = session.Images.FirstOrDefault(x => string.Equals(x.ImageId, imageId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Staged image was not found.");

            session.Images.Remove(image);
            DeleteImageFile(sessionId, image);
            await WriteSessionAsync(session, ct);
            return 0;
        }, ct);

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

        var path = FindImagePath(sessionId, image);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        Stream stream = File.OpenRead(path);
        return (stream, image.ContentType);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default) =>
        WithSessionLockAsync(sessionId, () =>
        {
            ct.ThrowIfCancellationRequested();
            var dir = GetSessionDir(sessionId);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }

            return Task.FromResult(0);
        }, ct);

    public async Task<int> DeleteExpiredSessionsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            ct.ThrowIfCancellationRequested();
            var sessionId = Path.GetFileName(dir);
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

    private async Task<T> WithSessionLockAsync<T>(string sessionId, Func<Task<T>> action, CancellationToken ct)
    {
        var key = SanitizeSessionId(sessionId);
        var gate = SessionLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
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
        var path = GetSessionMarkerPath(session.SessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, ct);
    }

    private string GetSessionDir(string sessionId) => Path.Combine(_root, SanitizeSessionId(sessionId));

    private string GetSessionMarkerPath(string sessionId) => Path.Combine(GetSessionDir(sessionId), "_session.json");

    private void DeleteImageFile(string sessionId, RecipeImportStagingImage image)
    {
        var path = FindImagePath(sessionId, image);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string? FindImagePath(string sessionId, RecipeImportStagingImage image)
    {
        var dir = GetSessionDir(sessionId);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        return Directory.EnumerateFiles(dir)
            .FirstOrDefault(f => Path.GetFileName(f).Contains(image.ImageId, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeSessionId(string sessionId)
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

    private static string ResolveRoot(RecipeFileStorageOptions options)
    {
        var basePath = (options.LocalBasePath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(basePath))
        {
            throw new InvalidOperationException(
                "Local recipe import staging requires RecipeFileStorage:LocalBasePath.");
        }

        return Path.Combine(Path.GetFullPath(basePath), "import-staging");
    }
}
