using System.Net.Http.Json;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class TagApiTests(SqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Search_ReturnsOk()
    {
        var response = await _client.GetAsync("/tags/search?q=te");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<TagLookupItem>>();
        Assert.NotNull(items);
    }
}
