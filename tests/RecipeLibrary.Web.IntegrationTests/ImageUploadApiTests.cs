using System.Net.Http.Headers;
using System.Text.Json;
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

        await using var uploadStream = await upload.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(uploadStream);
        Assert.True(document.RootElement.TryGetProperty("url", out var urlElement));
        var url = urlElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(url));

        var fileName = url!.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        Assert.False(string.IsNullOrWhiteSpace(fileName));

        var get = await _client.GetAsync($"/api/recipe-images/{fileName}");
        get.EnsureSuccessStatusCode();
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        var bytes = await get.Content.ReadAsByteArrayAsync();
        Assert.Equal(png.Length, bytes.Length);
    }
}
