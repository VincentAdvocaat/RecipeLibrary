using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Middleware;

/// <summary>
/// Redirects browser requests to the starting or failed page while persistence is not ready.
/// </summary>
public sealed class PersistenceReadinessMiddleware(RequestDelegate next)
{
    private static readonly PathString StartingPath = new("/starting");
    private static readonly PathString FailedPath = new("/failed");
    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString SystemReadinessPath = new("/api/system/readiness");
    private static readonly PathString CulturePath = new("/culture");
    private static readonly PathString MeasureSystemPath = new("/measure-system");
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
                status = readiness.State.ToString(),
                message = readiness.HasPermanentlyFailed
                    ? "The database failed to start. Check application logs."
                    : "The database is starting. Please retry shortly."
            });
            return;
        }

        var redirectPath = readiness.HasPermanentlyFailed ? FailedPath.Value! : StartingPath.Value!;
        context.Response.Redirect(redirectPath);
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments(StartingPath)
        || path.StartsWithSegments(FailedPath)
        || path.StartsWithSegments(HealthPath)
        || path.StartsWithSegments(SystemReadinessPath)
        || path.StartsWithSegments(CulturePath)
        || path.StartsWithSegments(MeasureSystemPath)
        || path.StartsWithSegments(BlazorPath)
        || path.StartsWithSegments(FrameworkPath)
        || path.StartsWithSegments(ContentPath)
        || path.StartsWithSegments("/Account")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".map", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) == true;

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
