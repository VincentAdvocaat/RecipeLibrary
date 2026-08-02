namespace RecipeLibrary.Web.Auth;

public static class OpenIddictAppConstants
{
    public const string MauiClientId = "maui-app";

    public const string ApiScope = "api";

    /// <summary>
    /// Policy scheme that selects cookie (Blazor) or OpenIddict validation (Bearer) per request.
    /// </summary>
    public const string CookieOrBearerScheme = "CookieOrBearer";
}
