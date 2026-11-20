using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public static class RecipeImportServiceRegistration
{
    public const string HttpClientName = "RecipeImport";

    /// <summary>Named client for OpenAI calls — no URL-fetch SSRF pin/timeout.</summary>
    public const string AiHttpClientName = "RecipeImportAi";

    public static IServiceCollection AddRecipeImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RecipeImportOptions>(configuration.GetSection(RecipeImportOptions.SectionName));
        var apiKey = configuration[$"{RecipeImportOptions.SectionName}:Ai:ApiKey"];
        if (!RecipeImportAiOptions.HasApiKey(apiKey))
        {
            var projectKey = configuration["OPENAI_API_KEY"];
            var endpoint = configuration[$"{RecipeImportOptions.SectionName}:Ai:Endpoint"];
            apiKey = IsOpenAiEndpoint(endpoint) && RecipeImportAiOptions.HasApiKey(projectKey) ? projectKey : null;
        }

        services.PostConfigure<RecipeImportOptions>(options => options.Ai.ApiKey = apiKey?.Trim());

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

        services.AddHttpClient(AiHttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecipeImportOptions>>().Value;
            var timeoutSeconds = options.Ai.TimeoutSeconds > 0 ? options.Ai.TimeoutSeconds : 120;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddScoped<IRecipeImportContentFetcher, RecipeImportContentFetcher>();
        services.AddScoped<IRecipeSocialCaptionFetcher, RecipeSocialCaptionFetcher>();
        services.AddScoped<IRecipeImageTextExtractor, TesseractRecipeImageTextExtractor>();
        services.AddScoped<IRecipeImportUrlGuard, RecipeImportUrlGuard>();

        var aiEnabled = configuration.GetValue<bool>($"{RecipeImportOptions.SectionName}:Ai:Enabled");
        if (aiEnabled && RecipeImportAiOptions.HasApiKey(apiKey))
        {
            services.AddScoped<IIngredientLineAiParser, OpenAiIngredientLineAiParser>();
            services.AddScoped<IRecipeAiParser, OpenAiRecipeAiParser>();
            services.AddScoped<IIngredientUnitConversionAiProposer, OpenAiIngredientUnitConversionAiProposer>();
        }
        else
        {
            services.AddScoped<IIngredientLineAiParser, NullIngredientLineAiParser>();
            services.AddScoped<IRecipeAiParser, NullRecipeAiParser>();
            services.AddScoped<IIngredientUnitConversionAiProposer, NullIngredientUnitConversionAiProposer>();
        }

        return services;
    }

    private static bool IsOpenAiEndpoint(string? endpoint) =>
        string.IsNullOrWhiteSpace(endpoint)
        || (Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443
            && uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase)
            && uri.UserInfo.Length == 0);
}
