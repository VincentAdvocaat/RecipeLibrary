using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Middleware;

/// <summary>
/// Redirects browser requests to the starting page while the database is still warming up.
/// </summary>
public sealed class PersistenceReadinessMiddleware(RequestDelegate next)
{
    private static readonly PathString StartingPath = new("/starting");
    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString BlazorPath = new("/_blazor");
    private static readonly PathString FrameworkPath = new("/_framework");
    private static readonly PathString ContentPath = new("/_content");

    public async Task InvokeAsync(HttpContext context, IPersistenceReadiness readiness)
    {
        if (readiness.IsReady || IsExempt(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (IsApiOrNonHtmlRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new
            {
                status = "Starting",
                message = "The database is starting. Please retry shortly."
            });
            return;
        }

        context.Response.Redirect(StartingPath.Value!);
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments(StartingPath)
        || path.StartsWithSegments(HealthPath)
        || path.StartsWithSegments(BlazorPath)
        || path.StartsWithSegments(FrameworkPath)
        || path.StartsWithSegments(ContentPath)
        || path.StartsWithSegments("/MicrosoftIdentity")
        || path.StartsWithSegments("/signin-oidc")
        || path.StartsWithSegments("/signout-oidc")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".map", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsApiOrNonHtmlRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api"))
        {
            return true;
        }

        var accept = request.Headers.Accept.ToString();
        if (string.IsNullOrEmpty(accept))
        {
            return false;
        }

        return !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
               && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
