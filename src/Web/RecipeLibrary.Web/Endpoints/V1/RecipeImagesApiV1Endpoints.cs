using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Endpoints.V1;

public static class RecipeImagesApiV1Endpoints
{
    public static RouteGroupBuilder MapRecipeImagesApiV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipe-images")
            .RequireAuthorization()
            .WithTags("RecipeImages");

        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadRecipeImageV1");

        group.MapGet("/{fileName}", GetAsync)
            .WithName("GetRecipeImageV1");

        return group;
    }

    private static async Task<Results<Ok<UploadRecipeImageResponse>, BadRequest<string>>> UploadAsync(
        IFormFile file,
        ICommandBus commandBus,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        await using var stream = file.OpenReadStream();
        var result = await commandBus.SendAsync<UploadRecipeImageCommand, UploadRecipeImageResult>(
            new UploadRecipeImageCommand
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
            },
            ct);

        // Prefer versioned URL for mobile clients; keep storage key compatible with legacy GET.
        var fileName = result.Url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? result.Url;
        var versionedUrl = $"/api/v1/recipe-images/{fileName}";
        return TypedResults.Ok(new UploadRecipeImageResponse(versionedUrl));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> GetAsync(
        string fileName,
        IQueryBus queryBus,
        IRecipeRepository recipeRepository,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.IndexOfAny(['/', '\\']) >= 0)
        {
            return TypedResults.NotFound();
        }

        var ownerUserId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(ownerUserId)
            || !await recipeRepository.IsRecipeImageAccessibleAsync(ownerUserId, fileName, ct))
        {
            return TypedResults.NotFound();
        }

        var result = await queryBus.QueryAsync<GetRecipeImageQuery, GetRecipeImageResult?>(
            new GetRecipeImageQuery { StorageKey = fileName },
            ct);
        if (result is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(result.Stream, result.ContentType);
    }
}

public sealed record UploadRecipeImageResponse(string Url);
