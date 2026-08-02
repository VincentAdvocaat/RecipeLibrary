using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Endpoints.V1;

public static class RecipesApiV1Endpoints
{
    public static RouteGroupBuilder MapRecipesApiV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes")
            .RequireAuthorization()
            .WithTags("Recipes");

        group.MapGet("/", ListAsync)
            .WithName("ListRecipes");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetRecipeById");

        group.MapPost("/", CreateAsync)
            .DisableAntiforgery()
            .WithName("CreateRecipe");

        group.MapPut("/{id:guid}", UpdateAsync)
            .DisableAntiforgery()
            .WithName("UpdateRecipe");

        group.MapDelete("/{id:guid}", DeleteAsync)
            .DisableAntiforgery()
            .WithName("DeleteRecipe");

        return group;
    }

    private static async Task<Ok<GetRecipeListResult>> ListAsync(
        IQueryBus queryBus,
        [FromQuery] string? search,
        [FromQuery] int? category,
        CancellationToken ct)
    {
        var result = await queryBus.QueryAsync<GetRecipeListQuery, GetRecipeListResult>(
            new GetRecipeListQuery { Search = search, Category = category },
            ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<GetRecipeByIdResult>, NotFound>> GetByIdAsync(
        Guid id,
        IQueryBus queryBus,
        CancellationToken ct)
    {
        var result = await queryBus.QueryAsync<GetRecipeByIdQuery, GetRecipeByIdResult?>(
            new GetRecipeByIdQuery { RecipeId = id },
            ct);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    private static async Task<Results<Created<CreateRecipeResult>, BadRequest<string>>> CreateAsync(
        [FromBody] CreateRecipeCommand command,
        ICommandBus commandBus,
        CancellationToken ct)
    {
        try
        {
            var result = await commandBus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(command, ct);
            return TypedResults.Created($"/api/v1/recipes/{result.RecipeId}", result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<UpdateRecipeResult>, NotFound, BadRequest<string>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateRecipeCommand command,
        ICommandBus commandBus,
        CancellationToken ct)
    {
        if (command.RecipeId != Guid.Empty && command.RecipeId != id)
        {
            return TypedResults.BadRequest("RecipeId in body must match the route id.");
        }

        var update = new UpdateRecipeCommand
        {
            RecipeId = id,
            Title = command.Title,
            PreparationTimeMinutes = command.PreparationTimeMinutes,
            CookingTimeMinutes = command.CookingTimeMinutes,
            Category = command.Category,
            Servings = command.Servings,
            Difficulty = command.Difficulty,
            Description = command.Description,
            ImageUrl = command.ImageUrl,
            Ingredients = command.Ingredients,
            InstructionSteps = command.InstructionSteps,
        };

        try
        {
            var result = await commandBus.SendAsync<UpdateRecipeCommand, UpdateRecipeResult>(update, ct);
            return TypedResults.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id,
        ICommandBus commandBus,
        CancellationToken ct)
    {
        var result = await commandBus.SendAsync<DeleteRecipeCommand, DeleteRecipeResult>(
            new DeleteRecipeCommand { RecipeId = id },
            ct);
        return result.Deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
