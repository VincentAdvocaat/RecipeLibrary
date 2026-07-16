namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class RecipeFileStorageOptions
{
    public const string SectionName = "RecipeFileStorage";

    /// <summary>Storage backend: <c>Local</c> (default) or <c>AzureBlob</c>.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Local disk base path when <see cref="Provider"/> is <c>Local</c>.</summary>
    public string? LocalBasePath { get; set; }

    public AzureBlobRecipeFileStorageOptions AzureBlob { get; set; } = new();
}

public sealed class AzureBlobRecipeFileStorageOptions
{
    /// <summary>Azure Storage account name (without .blob.core.windows.net).</summary>
    public string? AccountName { get; set; }

    /// <summary>Blob container for recipe images.</summary>
    public string ContainerName { get; set; } = "recipe-images";

    /// <summary>Blob container for short-lived OCR import staging images.</summary>
    public string StagingContainerName { get; set; } = "recipe-import-staging";
}
