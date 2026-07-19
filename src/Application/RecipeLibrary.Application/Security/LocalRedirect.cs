namespace RecipeLibrary.Application.Security;

/// <summary>
/// Validates same-origin relative redirect paths (blocks open redirects such as //evil.com).
/// </summary>
public static class LocalRedirect
{
    public static string Normalize(string? redirectUri, string fallback = "/")
    {
        if (string.IsNullOrWhiteSpace(redirectUri)
            || !redirectUri.StartsWith('/')
            || redirectUri.StartsWith("//", StringComparison.Ordinal)
            || redirectUri.Contains('\\', StringComparison.Ordinal)
            || redirectUri.Contains("://", StringComparison.Ordinal))
        {
            return fallback;
        }

        return redirectUri;
    }
}
