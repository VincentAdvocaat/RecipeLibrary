using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.ShoppingLists;

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

        services.AddScoped<IngredientMatcher>();
        services.AddSingleton<IIngredientSimilarityScorer, IngredientSimilarityScorer>();
        services.AddScoped<IngredientNameParser>();
        services.AddScoped<IngredientLineResolver>();
        services.AddScoped<ShoppingListIngredientMerger>();
        services.AddScoped<PantryIngredientMerger>();
        services.AddScoped<PantryExclusionFilter>();

        services.AddScoped<IngredientLineParser>();
        services.AddScoped<HtmlRecipeTextExtractor>();
        services.AddScoped<RecipeTextParser>();
        services.AddScoped<RecipeImportService>();
        services.AddScoped<IngredientQuantityConversionService>();

        RegisterHandlers(services, typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>
    /// Registers all non-abstract <see cref="ICommandHandler{TCommand,TResult}"/> and
    /// <see cref="IQueryHandler{TQuery,TResult}"/> implementations in the assembly.
    /// </summary>
    internal static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type is not { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            {
                continue;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var definition = iface.GetGenericTypeDefinition();
                if (definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>))
                {
                    services.AddScoped(iface, type);
                }
            }
        }
    }
}
