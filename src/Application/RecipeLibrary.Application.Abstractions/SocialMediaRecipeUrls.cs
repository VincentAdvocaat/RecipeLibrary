using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Detects Instagram / YouTube recipe post URLs and normalizes them for caption APIs.
/// </summary>
public static partial class SocialMediaRecipeUrls
{
    private static readonly HashSet<string> InstagramHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "instagram.com",
        "www.instagram.com",
    };

    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "youtu.be",
        "www.youtu.be",
    };

    public static bool IsInstagramPostUrl(Uri uri) =>
        TryGetInstagramCanonicalPostUrl(uri, out _);

    public static bool IsYouTubeVideoUrl(Uri uri) =>
        TryGetYouTubeVideoId(uri, out _);

    public static bool IsSocialRecipeUrl(Uri uri) =>
        IsInstagramPostUrl(uri) || IsYouTubeVideoUrl(uri);

    public static bool TryGetInstagramCanonicalPostUrl(
        Uri uri,
        [NotNullWhen(true)] out string? canonicalUrl)
    {
        canonicalUrl = null;
        if (!InstagramHosts.Contains(uri.Host))
        {
            return false;
        }

        // /reel/{code}/, /reels/{code}/, /p/{code}/, /tv/{code}/
        var match = InstagramPostPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            return false;
        }

        var kind = match.Groups["kind"].Value.ToLowerInvariant();
        if (kind == "reels")
        {
            kind = "reel";
        }

        var code = match.Groups["code"].Value;
        canonicalUrl = $"https://www.instagram.com/{kind}/{code}/";
        return true;
    }

    public static bool TryGetYouTubeVideoId(Uri uri, [NotNullWhen(true)] out string? videoId)
    {
        videoId = null;
        if (!YouTubeHosts.Contains(uri.Host))
        {
            return false;
        }

        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var shortId = uri.AbsolutePath.Trim('/');
            if (IsYouTubeVideoId(shortId))
            {
                videoId = shortId;
                return true;
            }

            return false;
        }

        var pathMatch = YouTubePathVideoIdRegex().Match(uri.AbsolutePath);
        if (pathMatch.Success && IsYouTubeVideoId(pathMatch.Groups["id"].Value))
        {
            videoId = pathMatch.Groups["id"].Value;
            return true;
        }

        if (TryGetQueryValue(uri, "v", out var fromQuery) && IsYouTubeVideoId(fromQuery))
        {
            videoId = fromQuery;
            return true;
        }

        return false;
    }

    private static bool TryGetQueryValue(Uri uri, string key, [NotNullWhen(true)] out string? value)
    {
        value = null;
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        var span = query.AsSpan().TrimStart('?');
        while (!span.IsEmpty)
        {
            var amp = span.IndexOf('&');
            var pair = amp >= 0 ? span[..amp] : span;
            span = amp >= 0 ? span[(amp + 1)..] : [];

            var eq = pair.IndexOf('=');
            var name = eq >= 0 ? pair[..eq] : pair;
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = eq >= 0 ? pair[(eq + 1)..] : [];
            try
            {
                value = Uri.UnescapeDataString(raw.ToString());
            }
            catch (UriFormatException)
            {
                return false;
            }

            return value.Length > 0;
        }

        return false;
    }

    private static bool IsYouTubeVideoId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && YouTubeVideoIdRegex().IsMatch(value);

    [GeneratedRegex(
        @"^/(?<kind>reel|reels|p|tv)/(?<code>[A-Za-z0-9_-]+)/?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex InstagramPostPathRegex();

    [GeneratedRegex(
        @"^/(?:shorts|embed|live|v)/(?<id>[A-Za-z0-9_-]{11})/?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex YouTubePathVideoIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex YouTubeVideoIdRegex();
}
