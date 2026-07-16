using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.RecipeImages;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class GetRecipeImageQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsNull_ForInvalidKey()
    {
        var sut = new GetRecipeImageQueryHandler(new FakeRecipeFileStorage());

        Assert.Null(await sut.HandleAsync(new GetRecipeImageQuery { StorageKey = "../secret.png" }));
        Assert.Null(await sut.HandleAsync(new GetRecipeImageQuery { StorageKey = "file.txt" }));
    }

    [Fact]
    public async Task HandleAsync_ReturnsStream_WhenStorageHasFile()
    {
        using var stream = new MemoryStream([0x01]);
        var storage = new FakeRecipeFileStorage(("photo.png", stream, "image/png"));
        var sut = new GetRecipeImageQueryHandler(storage);

        var result = await sut.HandleAsync(new GetRecipeImageQuery { StorageKey = "photo.png" });

        Assert.NotNull(result);
        Assert.Equal("image/png", result!.ContentType);
    }

    private sealed class FakeRecipeFileStorage((string Key, Stream Stream, string ContentType)? file = null) : IRecipeFileStorage
    {
        public Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult("/api/recipe-images/x");

        public Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default)
        {
            if (file is null || file.Value.Key != storageKey)
            {
                return Task.FromResult<(Stream, string)?>(null);
            }

            return Task.FromResult<(Stream, string)?>((file.Value.Stream, file.Value.ContentType));
        }
    }
}
