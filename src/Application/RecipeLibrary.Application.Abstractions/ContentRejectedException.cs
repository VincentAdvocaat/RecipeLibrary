namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Thrown when automated moderation blocks persist (severity at or above block threshold).
/// UI should show a localized message; do not surface category details to end users.
/// </summary>
public sealed class ContentRejectedException : Exception
{
    public ContentRejectedException()
        : base("Content was rejected by moderation.")
    {
    }
}
