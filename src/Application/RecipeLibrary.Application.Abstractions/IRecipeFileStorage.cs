namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Abstraction for storing and retrieving recipe image files.
/// Implementations may use local disk, Azure Blob, FTP, etc.
/// Used by application handlers only; the Web layer uses CommandBus/QueryBus.
/// </summary>
public interface IRecipeFileStorage
{
    /// <summary>
    /// Saves the image and returns the URL path to store in the database (e.g. /api/recipe-images/{storageKey}).
    /// Storage keys are prefixed with <paramref name="ownerUserId"/> so pending uploads stay private.
    /// </summary>
    Task<string> SaveAsync(
        Stream content,
        string suggestedFileName,
        string contentType,
        string ownerUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Opens the image by storage key. Returns null if not found. Caller is responsible for disposing the stream.
    /// </summary>
    Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default);
}
