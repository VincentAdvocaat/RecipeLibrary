using System.Net;
using System.Net.Http.Json;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class IngredientApiTests(SqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Search_ReturnsSeededIngredient()
    {
        var response = await _client.GetAsync("/ingredients/search?q=ge");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<IngredientLookupItem>>();
        Assert.NotNull(items);
        Assert.Contains(items, i => i.Name.Contains("Gehakt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Match_ReturnsResultForKnownInput()
    {
        var response = await _client.PostAsJsonAsync("/ingredients/match", new MatchIngredientCommand { Input = "gehakt" });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MatchIngredientResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Ingredient);
    }

    [Fact]
    public async Task ParseLine_SplitsPreparation()
    {
        var response = await _client.PostAsJsonAsync("/ingredients/parse-line", new ParseIngredientLineRequest { Input = "ui fijn gesneden" });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ParseIngredientLineResult>();
        Assert.NotNull(result);
        Assert.Equal("ui", result.Name);
        Assert.Equal("fijn gesneden", result.Preparation);
    }
}
