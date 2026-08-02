using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.ContentModeration;

public static class ContentModerationServiceRegistration
{
    public static IServiceCollection AddContentModeration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContentModerationOptions>(
            configuration.GetSection(ContentModerationOptions.SectionName));

        var enabled = configuration.GetValue<bool>($"{ContentModerationOptions.SectionName}:Enabled");
        var endpoint = configuration[$"{ContentModerationOptions.SectionName}:Endpoint"];
        var apiKey = configuration[$"{ContentModerationOptions.SectionName}:ApiKey"];

        if (enabled
            && !string.IsNullOrWhiteSpace(endpoint)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton(_ =>
                new ContentSafetyClient(new Uri(endpoint), new AzureKeyCredential(apiKey)));
            services.AddScoped<IContentModerator, AzureContentModerator>();
        }
        else
        {
            services.AddSingleton<IContentModerator, NullContentModerator>();
        }

        services.AddScoped<IContentModerationStore, EfContentModerationStore>();
        services.AddHostedService<ContentModerationAdminSeedHostedService>();
        return services;
    }
}
