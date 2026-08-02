namespace RecipeLibrary.Web.Auth;

public static class OpenIddictAppConstants
{
    public const string MauiClientId = "maui-app";

    public const string ApiScope = "api";

    /// <summary>
    /// Scheme that tries Bearer then falls back to the Identity application cookie.
    /// </summary>
    public const string CookieOrBearerScheme = "CookieOrBearer";

    /// <summary>
    /// Authenticated callers; Bearer tokens must include the <see cref="ApiScope"/> scope.
    /// Cookie (Blazor) principals are allowed without OpenIddict scopes.
    /// </summary>
    public const string ApiV1Policy = "ApiV1";
}
