using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Infrastructure.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class OpenAiRecipeImportTests
{
    [Fact]
    public async Task FullRecipe_UsesSolAndStrictSchema_AndPreservesIncompleteSource()
    {
        const string recipe = """
            {"title":"Pears","description":null,"preparationTimeMinutes":null,"cookingTimeMinutes":null,"servings":null,
             "ingredients":[{"rawLine":"2 pears","name":"pears","quantity":2,"unit":"Piece","preparation":null,"confidence":0.97}],
             "steps":[]}
            """;
        var handler = new OpenAiHandler(recipe);
        var parser = new OpenAiRecipeAiParser(handler, ConfiguredOptions());

        var result = await parser.ParseAsync("Pears\nIngredients\n2 pears");

        Assert.Equal("Pears", result.Title);
        Assert.Null(result.CookingTimeMinutes);
        Assert.Empty(result.Steps);
        Assert.Equal("pears", Assert.Single(result.Ingredients).Name);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var body = request.RootElement;
        Assert.Equal("gpt-6.2-sol", body.GetProperty("model").GetString());
        Assert.Equal("low", body.GetProperty("reasoning_effort").GetString());
        Assert.Equal(6000, body.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(body.GetProperty("store").GetBoolean());
        Assert.False(body.TryGetProperty("temperature", out _));
        Assert.True(body.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task IngredientBatch_RejectsUnexpectedLineOrder()
    {
        const string recipe = """
            {"ingredients":[{"rawLine":"3 pears","quantity":3,"unit":"Piece","name":"pears","preparation":null,"confidence":0.9}]}
            """;
        var parser = new OpenAiIngredientLineAiParser(new OpenAiHandler(recipe), ConfiguredOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseLinesAsync(["2 pears"]));
    }

    [Theory]
    [InlineData("UNSET", "project-key", "project-key")]
    [InlineData("app-key", "project-key", "app-key")]
    [InlineData("UNSET", null, null)]
    public void Configuration_UsesUsableProjectKeyButPrefersAppKey(
        string appKey, string? projectKey, string? expected)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RecipeImport:Ai:Enabled"] = "true",
            ["RecipeImport:Ai:ApiKey"] = appKey,
            ["OPENAI_API_KEY"] = projectKey,
        }).Build();
        using var provider = new ServiceCollection().AddRecipeImport(settings).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RecipeImportOptions>>().Value.Ai;

        Assert.Equal(expected, options.ApiKey);
        Assert.Equal(expected is not null, options.IsConfigured);
    }

    private static IOptions<RecipeImportOptions> ConfiguredOptions() => Options.Create(new RecipeImportOptions
    {
        Ai = new RecipeImportAiOptions { Enabled = true, ApiKey = "example-key" },
    });

    private sealed class OpenAiHandler(string modelContent) : HttpMessageHandler, IHttpClientFactory
    {
        public string? RequestBody { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content = modelContent } } },
                })),
            };
        }
    }
}
