namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Fetches recipe text from social platforms where the HTML page does not expose the caption
/// (Instagram reels/posts, YouTube Shorts/videos).
/// </summary>
public interface IRecipeSocialCaptionFetcher
{
    /// <summary>
    /// When the URL is a supported Instagram or YouTube post, returns the caption/description
    /// as plain text. Otherwise returns <c>null</c> so callers can fall back to HTML scraping.
    /// </summary>
    Task<string?> TryFetchCaptionAsync(string url, CancellationToken ct = default);
}
