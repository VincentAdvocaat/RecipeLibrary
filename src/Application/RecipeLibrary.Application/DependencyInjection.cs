using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Application.UseCases.RecipeImages;
using RecipeLibrary.Application.UseCases.Recipes;

namespace RecipeLibrary.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<InProcessBus>();
        services.AddScoped<ICommandBus>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddScoped<IQueryBus>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddSingleton<IIngredientTextNormalizer, IngredientTextNormalizer>();

        services.AddScoped<ICommandHandler<CreateRecipeCommand, CreateRecipeResult>, CreateRecipeCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecipeListQuery, GetRecipeListResult>, GetRecipeListQueryHandler>();
        services.AddScoped<ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>, UploadRecipeImageCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecipeImageQuery, GetRecipeImageResult?>, GetRecipeImageQueryHandler>();
        services.AddScoped<ICommandHandler<MatchIngredientCommand, MatchIngredientResult>, MatchIngredientCommandHandler>();
        services.AddScoped<IQueryHandler<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>, SearchIngredientsQueryHandler>();
        services.AddScoped<ICommandHandler<AddIngredientTagsCommand, AddIngredientTagsResult>, AddIngredientTagsCommandHandler>();
        services.AddScoped<IQueryHandler<SearchTagsQuery, IReadOnlyList<TagLookupItem>>, SearchTagsQueryHandler>();
        services.AddScoped<IngredientMatcher>();
        services.AddScoped<IngredientNameParser>();

        return services;
    }
}

