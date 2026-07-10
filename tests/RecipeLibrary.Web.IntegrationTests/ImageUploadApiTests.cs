using System.Net.Http.Headers;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class ImageUploadApiTests(SqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UploadAndRetrieveImage_Works()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(png);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        var upload = await _client.PostAsync("/api/upload-recipe-image", content);
        upload.EnsureSuccessStatusCode();

        var json = await upload.Content.ReadAsStringAsync();
        Assert.Contains("url", json, StringComparison.OrdinalIgnoreCase);

        var fileName = json.Split('/').Last().Trim().TrimEnd('"', '}');
        var get = await _client.GetAsync($"/api/recipe-images/{fileName}");
        get.EnsureSuccessStatusCode();
    }
}
