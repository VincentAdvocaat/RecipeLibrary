using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.RecipeImages;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class UploadRecipeImageCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_SavesImageAndReturnsUrl()
    {
        using var content = new MemoryStream([0x01, 0x02]);
        var storage = new FakeRecipeFileStorage("/api/recipe-images/test.png");
        var sut = new UploadRecipeImageCommandHandler(storage, new FixedCurrentUser("user-1"));

        var result = await sut.HandleAsync(new UploadRecipeImageCommand
        {
            Content = content,
            FileName = "photo.png",
            ContentType = "image/png",
        });

        Assert.Equal("/api/recipe-images/test.png", result.Url);
        Assert.Equal("photo.png", storage.LastFileName);
        Assert.Equal("user-1", storage.LastOwnerUserId);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenUnauthenticated()
    {
        using var content = new MemoryStream([0x01]);
        var sut = new UploadRecipeImageCommandHandler(
            new FakeRecipeFileStorage("unused"),
            new AnonymousCurrentUser());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UploadRecipeImageCommand
            {
                Content = content,
                FileName = "photo.png",
                ContentType = "image/png",
            }));
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenFileNameMissing()
    {
        using var content = new MemoryStream([0x01]);
        var sut = new UploadRecipeImageCommandHandler(
            new FakeRecipeFileStorage("unused"),
            new FixedCurrentUser("user-1"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new UploadRecipeImageCommand
            {
                Content = content,
                FileName = "",
                ContentType = "image/png",
            }));
    }

    private sealed class FakeRecipeFileStorage(string url) : IRecipeFileStorage
    {
        public string? LastFileName { get; private set; }
        public string? LastOwnerUserId { get; private set; }

        public Task<string> SaveAsync(
            Stream content,
            string suggestedFileName,
            string contentType,
            string ownerUserId,
            CancellationToken ct = default)
        {
            LastFileName = suggestedFileName;
            LastOwnerUserId = ownerUserId;
            return Task.FromResult(url);
        }

        public Task<(Stream Stream, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default) =>
            Task.FromResult<(Stream, string)?>(null);
    }
}
