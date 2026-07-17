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
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Validate each redirect hop against SSRF rules in RecipeImportContentFetcher.
                AllowAutoRedirect = false,
                // Connect only to pre-validated (or freshly filtered) public addresses — no DNS rebinding.
                ConnectCallback = RecipeImportSocketsConnect.ConnectAsync,
            });

        services.AddScoped<IRecipeImportContentFetcher, RecipeImportContentFetcher>();
        services.AddScoped<IRecipeSocialCaptionFetcher, RecipeSocialCaptionFetcher>();
        services.AddScoped<IRecipeImageTextExtractor, TesseractRecipeImageTextExtractor>();

        var aiEnabled = configuration.GetValue<bool>($"{RecipeImportOptions.SectionName}:Ai:Enabled");
        var apiKey = configuration[$"{RecipeImportOptions.SectionName}:Ai:ApiKey"];
        if (aiEnabled && !string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddScoped<IIngredientLineAiParser, OpenAiIngredientLineAiParser>();
            services.AddScoped<IRecipeAiParser, OpenAiRecipeAiParser>();
        }
        else
        {
            services.AddScoped<IIngredientLineAiParser, NullIngredientLineAiParser>();
            services.AddScoped<IRecipeAiParser, NullRecipeAiParser>();
        }

        return services;
    }
}
