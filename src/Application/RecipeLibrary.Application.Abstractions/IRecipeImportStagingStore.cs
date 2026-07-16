namespace RecipeLibrary.Application.Abstractions;

public sealed class RecipeImportStagingSession
{
    public string SessionId { get; init; } = string.Empty;

    public string OwnerKey { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ExpiresUtc { get; init; }

    public List<RecipeImportStagingImage> Images { get; init; } = [];
}

public sealed class RecipeImportStagingImage
{
    public string ImageId { get; init; } = string.Empty;

    public int Order { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }
}

public interface IRecipeImportStagingStore
{
    Task<RecipeImportStagingSession> CreateSessionAsync(string ownerKey, TimeSpan ttl, CancellationToken ct = default);

    Task<RecipeImportStagingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    Task<RecipeImportStagingImage> AddImageAsync(
        string sessionId,
        string ownerKey,
        Stream content,
        string fileName,
        string contentType,
        int maxImages,
        CancellationToken ct = default);

    Task RemoveImageAsync(string sessionId, string ownerKey, string imageId, CancellationToken ct = default);

    Task<(Stream Stream, string ContentType)?> OpenImageAsync(
        string sessionId,
        string ownerKey,
        string imageId,
        CancellationToken ct = default);

    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Deletes sessions whose ExpiresUtc is in the past. Returns number of sessions removed.</summary>
    Task<int> DeleteExpiredSessionsAsync(CancellationToken ct = default);
}
