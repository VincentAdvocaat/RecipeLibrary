using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.Validators;

public static class UploadRecipeImageCommandValidator
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static void ValidateAndThrow(UploadRecipeImageCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Content);

        var fileName = (command.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(command));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Invalid image type. Use jpg, png, gif or webp.", nameof(command));
        }

        var contentType = (command.ContentType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Content type must be an image type.", nameof(command));
        }

        if (command.Content.CanSeek && command.Content.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.", nameof(command));
        }
    }
}
