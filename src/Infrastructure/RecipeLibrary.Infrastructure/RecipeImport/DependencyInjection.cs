using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public static class RecipeImportServiceRegistration
{
    public const string HttpClientName = "RecipeImport";

    public static IServiceCollection AddRecipeImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RecipeImportOptions>(configuration.GetSection(RecipeImportOptions.SectionName));

        services.AddHttpClient(HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecipeImportOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.UrlFetch.TimeoutSeconds);
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json");
        });

        services.AddScoped<IRecipeImportContentFetcher, RecipeImportContentFetcher>();
        services.AddScoped<IRecipeImageTextExtractor, TesseractRecipeImageTextExtractor>();

        var aiEnabled = configuration.GetValue<bool>($"{RecipeImportOptions.SectionName}:Ai:Enabled");
        var apiKey = configuration[$"{RecipeImportOptions.SectionName}:Ai:ApiKey"];
        if (aiEnabled && !string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddScoped<IIngredientLineAiParser, OpenAiIngredientLineAiParser>();
        }
        else
        {
            services.AddScoped<IIngredientLineAiParser, NullIngredientLineAiParser>();
        }

        return services;
    }
}
