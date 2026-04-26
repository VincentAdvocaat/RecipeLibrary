using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public static class FileStorageServiceRegistration
{
    /// <summary>
    /// Registers <see cref="IRecipeFileStorage"/> with <see cref="LocalRecipeFileStorage"/>.
    /// Binds options from configuration key "RecipeFileStorage" (e.g. RecipeFileStorage:LocalBasePath).
    /// When the configured BasePath is null or empty, <paramref name="defaultBasePath"/> is used (e.g. a folder outside the repo).
    /// </summary>
    public static IServiceCollection AddRecipeFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string? defaultBasePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<LocalRecipeFileStorageOptions>(configuration.GetSection("RecipeFileStorage"));

        if (!string.IsNullOrWhiteSpace(defaultBasePath))
        {
            var path = defaultBasePath;
            services.PostConfigure<LocalRecipeFileStorageOptions>(o =>
            {
                if (string.IsNullOrWhiteSpace(o.BasePath))
                {
                    o.BasePath = path;
                }
            });
        }

        services.AddScoped<IRecipeFileStorage, LocalRecipeFileStorage>();
        return services;
    }
}
