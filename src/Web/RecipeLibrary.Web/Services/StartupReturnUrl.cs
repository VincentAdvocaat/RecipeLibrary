namespace RecipeLibrary.Web.Services;

public static class StartupReturnUrl
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '/' || value.StartsWith("//")
            || value.Any(c => char.IsControl(c) || c == '\\')) return "/";

        // Decode before checking, to reject encoded authority separators and startup loops.
        string decoded;
        try { decoded = Uri.UnescapeDataString(value); }
        catch (UriFormatException) { return "/"; }
        if (decoded.StartsWith("//") || decoded.Any(c => char.IsControl(c) || c == '\\')) return "/";
        if (!Uri.TryCreate(new Uri("https://local.invalid"), value, out var destination)
            || destination.Host != "local.invalid") return "/";
        var path = Uri.UnescapeDataString(destination.AbsolutePath).TrimEnd('/');
        if (path.Equals("/starting", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/failed", StringComparison.OrdinalIgnoreCase)) return "/";
        return destination.PathAndQuery + destination.Fragment;
    }
}
