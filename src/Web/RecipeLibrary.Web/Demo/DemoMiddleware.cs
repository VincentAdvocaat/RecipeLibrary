using Microsoft.AspNetCore.Antiforgery;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Demo;

public sealed class DemoMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, DemoIdentity identity, DemoOperationRunner guard)
    {
        try
        {
            var path = http.Request.Path;
            var publicPath = path.StartsWithSegments("/demo") || path.StartsWithSegments("/Account")
                || path.StartsWithSegments("/health") || path.StartsWithSegments("/api/system")
                || path.StartsWithSegments("/starting") || path.StartsWithSegments("/failed")
                || path.StartsWithSegments("/terms") || path.StartsWithSegments("/_framework")
                || path.StartsWithSegments("/_content") || path.StartsWithSegments("/images")
                || path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true
                || path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true;
            if (!publicPath)
            {
                if (identity.IsDemo)
                {
                    await guard.GetValidSessionAsync(http.RequestAborted);
                    if (http.Request.Method == "POST" && (path.StartsWithSegments("/recipes/import")
                        || path.StartsWithSegments("/recipes/import-url") || path.StartsWithSegments("/recipes/import-image")
                        || path.StartsWithSegments("/api/upload-recipe-image")
                        || (path.StartsWithSegments("/ingredients") && path.Value!.EndsWith("/tags", StringComparison.Ordinal))))
                        throw new DemoAccessException("Demo.AccountRequired");
                }
                else if (!identity.IsAuthenticated && http.Request.Cookies.ContainsKey(DemoEndpoints.MarkerCookie)
                    && (path == "/" || path.StartsWithSegments("/recipes") || path.StartsWithSegments("/shopping-list") || path.StartsWithSegments("/pantry")))
                    throw new DemoAccessException("Demo.Expired");
            }
            await next(http);
        }
        catch (AntiforgeryValidationException)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        catch (DemoAccessException ex)
        {
            http.Response.Headers.CacheControl = "no-store";
            if (ex.Code == "Demo.Limit") http.Response.Headers.RetryAfter = "3600";
            var formNavigation = http.Request.HasFormContentType && http.Request.Path.StartsWithSegments("/demo");
            var htmlNavigation = http.Request.Method == "GET" && http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
            if (formNavigation || htmlNavigation)
            {
                http.Response.Redirect("/demo?state=" + ex.Code.Split('.').Last().ToLowerInvariant());
                return;
            }
            http.Response.StatusCode = ex.Code == "Demo.Limit" ? 429 : 403;
            await http.Response.WriteAsJsonAsync(new { error = ex.Code }, http.RequestAborted);
        }
    }
}
