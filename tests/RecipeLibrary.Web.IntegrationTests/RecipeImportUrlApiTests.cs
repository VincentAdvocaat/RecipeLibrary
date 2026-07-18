using System.Net;
using System.Net.Http.Json;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class RecipeImportUrlApiTests(SqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData("http://127.0.0.1/recipe")]
    [InlineData("https://localhost/recipe")]
    [InlineData("http://192.168.1.10/recipe")]
    [InlineData("http://10.0.0.5/recipe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public async Task ImportUrl_ReturnsBadRequest_ForPrivateOrLoopbackHosts(string url)
    {
        var response = await _client.PostAsJsonAsync(
            "/recipes/import-url",
            new ImportRecipeFromUrlQuery { Url = url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }
}
