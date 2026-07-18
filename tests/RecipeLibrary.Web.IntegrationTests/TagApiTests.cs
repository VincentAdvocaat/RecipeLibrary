using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class TagApiTests(SqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Search_ReturnsSeededTag()
    {
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
            await bus.SendAsync<AddIngredientTagsCommand, AddIngredientTagsResult>(
                new AddIngredientTagsCommand
                {
                    IngredientId = fixture.Seed.GehaktCanonicalId,
                    Tags = ["Weekmenu"],
                });
        }

        var response = await _client.GetAsync("/tags/search?q=week");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<TagLookupItem>>();
        Assert.NotNull(items);
        Assert.Contains(items, t => t.Name.Contains("Weekmenu", StringComparison.OrdinalIgnoreCase));
    }
}
