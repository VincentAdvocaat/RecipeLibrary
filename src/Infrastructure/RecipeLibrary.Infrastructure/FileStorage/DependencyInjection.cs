using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public static class FileStorageServiceRegistration
{
    /// <summary>
    /// Registers <see cref="IRecipeFileStorage"/> using local disk or Azure Blob based on
    /// <c>RecipeFileStorage:Provider</c> (<c>Local</c> default, or <c>AzureBlob</c> for production).
    /// </summary>
    public static IServiceCollection AddRecipeFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string? defaultBasePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RecipeFileStorageOptions>(configuration.GetSection(RecipeFileStorageOptions.SectionName));
        services.Configure<LocalRecipeFileStorageOptions>(configuration.GetSection(RecipeFileStorageOptions.SectionName));
        services.AddOptions<LocalRecipeFileStorageOptions>()
            .Configure<IOptions<RecipeFileStorageOptions>>((local, fileStorage) =>
            {
                if (string.IsNullOrWhiteSpace(local.BasePath) && !string.IsNullOrWhiteSpace(fileStorage.Value.LocalBasePath))
                {
                    local.BasePath = fileStorage.Value.LocalBasePath;
                }
            });

        if (!string.IsNullOrWhiteSpace(defaultBasePath))
        {
            var path = defaultBasePath;
            services.PostConfigure<RecipeFileStorageOptions>(o =>
            {
                if (string.IsNullOrWhiteSpace(o.LocalBasePath))
                {
                    o.LocalBasePath = path;
                }
            });
            services.PostConfigure<LocalRecipeFileStorageOptions>(o =>
            {
                if (string.IsNullOrWhiteSpace(o.BasePath))
                {
                    o.BasePath = path;
                }
            });
        }

        services.AddScoped<IRecipeFileStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RecipeFileStorageOptions>>().Value;
            return string.Equals(options.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase)
                ? new AzureBlobRecipeFileStorage(sp.GetRequiredService<IOptions<RecipeFileStorageOptions>>())
                : sp.GetRequiredService<LocalRecipeFileStorage>();
        });
        services.AddScoped<LocalRecipeFileStorage>();

        return services;
    }
}
