namespace RecipeLibrary.Domain.ValueObjects;

public static class RecipeSourceUrl
{
    public const int MaxLength = 2048;

    public static bool TryNormalize(string? input, out string? sourceUrl)
    {
        sourceUrl = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var value = input.Trim();
        if (value.Length > MaxLength
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.IsWellFormedOriginalString()
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Host.Length == 0
            || uri.UserInfo.Length != 0)
        {
            return false;
        }

        var normalized = uri.AbsoluteUri;
        if (normalized.Length > MaxLength)
        {
            return false;
        }

        sourceUrl = normalized;
        return true;
    }

    public static string? Normalize(string? input)
    {
        if (!TryNormalize(input, out var sourceUrl))
        {
            throw new ArgumentException("Source URL must be an absolute HTTP or HTTPS link without credentials.", nameof(input));
        }

        return sourceUrl;
    }
}
