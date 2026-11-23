using Microsoft.AspNetCore.Antiforgery;
using RecipeLibrary.Application.UseCases.Recipes;

namespace RecipeLibrary.Web.Endpoints;

public static class SharedRecipeEndpoints
{
    public const string Route = "/shared-recipe";
    public const string ImageRoute = Route + "/image";
    public const string SaveRoute = Route + "/save";

    public static string ViewPath(Guid key) => $"{Route}?key={key:D}";

    public static void MapSharedRecipes(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ImageRoute, GetImageAsync).AllowAnonymous();
        endpoints.MapPost(SaveRoute, SaveCopyAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetImageAsync(
        string? key, HttpContext http, SharedRecipeService recipes, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!Guid.TryParseExact(key, "D", out var linkKey))
        {
            return Results.NotFound();
        }

        var image = await recipes.GetImageAsync(linkKey, ct);
        return image is null ? Results.NotFound() : Results.File(image.Stream, image.ContentType);
    }

    private static async Task<IResult> SaveCopyAsync(
        string? key, HttpContext http, IAntiforgery antiforgery, SharedRecipeService recipes, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!await antiforgery.IsRequestValidAsync(http))
        {
            return Results.BadRequest();
        }

        if (!Guid.TryParseExact(key, "D", out var linkKey))
        {
            return Results.NotFound();
        }

        if (!recipes.CanSave)
        {
            return Results.LocalRedirect($"/Account/Login?returnUrl={Uri.EscapeDataString(ViewPath(linkKey))}");
        }

        var recipeId = await recipes.SaveCopyAsync(linkKey, ct);
        return Results.LocalRedirect(recipeId is null ? ViewPath(linkKey) : $"/recipes/{recipeId:D}");
    }
}
