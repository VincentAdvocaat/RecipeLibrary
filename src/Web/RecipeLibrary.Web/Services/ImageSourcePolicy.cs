using RecipeLibrary.Infrastructure.FileStorage;

namespace RecipeLibrary.Web.Services;

public sealed record ImageDisplaySource(string? AutomaticSource, Uri? ExternalSource);

public static class ImageSourcePolicy
{
    public static ImageDisplaySource Classify(string? source, RecipeFileStorageOptions storage)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Any(char.IsControl) || source.Contains('\\'))
            return new(null, null);

        if (source.StartsWith('/') && !source.StartsWith("//", StringComparison.Ordinal))
        {
            var decoded = Uri.UnescapeDataString(source);
            if (decoded.Any(char.IsControl) || decoded.Contains('\\') || decoded.StartsWith("//", StringComparison.Ordinal)
                || decoded.Split('/', '?', '#').Any(segment => segment is "." or "..")) return new(null, null);
            if (Uri.TryCreate(new Uri("https://local.invalid"), source, out var local)
                && local.Host == "local.invalid"
                && (local.AbsolutePath.StartsWith("/images/demo/", StringComparison.Ordinal)
                    || local.AbsolutePath.StartsWith("/uploads/recipe-images/", StringComparison.Ordinal)
                    || local.AbsolutePath.StartsWith("/api/recipe-images/", StringComparison.Ordinal)
                    || local.AbsolutePath.StartsWith("/api/v1/recipe-images/", StringComparison.Ordinal)))
                return new(source, null);
            return new(null, null);
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) return new(null, null);

        var account = storage.AzureBlob.AccountName?.Trim();
        var trustedAccount = account is { Length: >= 3 and <= 24 }
            && account.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');
        if (string.Equals(storage.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase)
            && trustedAccount && uri.IsDefaultPort
            && string.Equals(uri.IdnHost, $"{account}.blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            return new(uri.AbsoluteUri, null);

        return new(null, uri);
    }
}
