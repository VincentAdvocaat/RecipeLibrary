using Microsoft.AspNetCore.Antiforgery;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Services;

public static class ApplicationStartEndpoints
{
    public static void MapApplicationStart(this WebApplication app, string[] allowedOrigins)
    {
        app.MapGet("/api/system/start-token", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        }).AllowAnonymous();

        app.MapPost("/api/system/start", async (HttpContext context, IAntiforgery antiforgery,
            ApplicationWakeRequest wake, IPersistenceReadiness readiness, DatabaseAvailability availability) =>
        {
            var origin = context.Request.Headers.Origin.ToString();
            var ownOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            var external = !string.IsNullOrEmpty(origin) && !string.Equals(origin, ownOrigin, StringComparison.OrdinalIgnoreCase);
            if (external)
            {
                // Exact trusted static-site origin plus a non-simple header forces CORS preflight.
                // Cross-site forms cannot supply this header. No cookies or credentials are accepted here.
                if (!allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                    || context.Request.Headers["X-Application-Start"] != "user") return Results.Json(new { error = "origin_rejected" }, statusCode: 403);
            }
            else
            {
                try { await antiforgery.ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException) { return Results.Json(new { error = "invalid_antiforgery_token" }, statusCode: 400); }
            }
            context.Response.Headers.CacheControl = "no-store";
            var wasReady = readiness.IsReady;
            wake.Request("ExplicitApplicationStart", context.TraceIdentifier);
            var state = wasReady ? await availability.ProbeForStartAsync(context.RequestAborted) : readiness.State;
            return Results.Json(new { state = state.ToString(), traceId = context.TraceIdentifier }, statusCode: 202);
        }).AllowAnonymous().RequireCors("WelcomeReadiness").RequireRateLimiting("ApplicationStart");
    }
}
