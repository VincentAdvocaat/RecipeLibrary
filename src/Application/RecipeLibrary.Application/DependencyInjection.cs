using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
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

        services.AddScoped<ICommandHandler<CreateRecipeCommand, CreateRecipeResult>, CreateRecipeCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecipeListQuery, GetRecipeListResult>, GetRecipeListQueryHandler>();

        return services;
    }
}

