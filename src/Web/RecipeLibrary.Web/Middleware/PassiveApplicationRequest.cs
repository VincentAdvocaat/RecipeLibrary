namespace RecipeLibrary.Web.Middleware;

/// <summary>Requests that must remain usable without validating SQL-backed Identity/demo state.</summary>
public static class PassiveApplicationRequest
{
    public static bool IsPassive(HttpContext context)
    {
        var path = context.Request.Path;
        return context.GetEndpoint() is null
            || path == "/"
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/api/system")
            || path.StartsWithSegments("/starting")
            || path.StartsWithSegments("/failed")
            || path.StartsWithSegments("/not-found")
            || path.StartsWithSegments("/Error")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || (context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null
                && Path.HasExtension(path.Value));
    }
}
