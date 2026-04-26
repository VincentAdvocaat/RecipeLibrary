namespace RecipeLibrary.Infrastructure.FileStorage;

/// <summary>
/// Options for local file-based recipe image storage.
/// BasePath defaults to a folder outside the repo when not set (see AddRecipeFileStorage).
/// </summary>
public sealed class LocalRecipeFileStorageOptions
{
    /// <summary>
    /// Base directory for stored files. Recipe images are stored under BasePath/recipe-images/.
    /// If not set, the registration default is used (e.g. RecipeLibraryUploads outside content root).
    /// </summary>
    public string? BasePath { get; set; }
}
