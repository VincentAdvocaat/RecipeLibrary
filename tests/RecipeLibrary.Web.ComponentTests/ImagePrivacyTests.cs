using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Components.Atoms;
using RecipeLibrary.Components.Molecules;
using RecipeLibrary.Components.Organisms;
using RecipeLibrary.Infrastructure.FileStorage;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class ImagePrivacyTests : ComponentTestContext
{
    public ImagePrivacyTests()
    {
        // Match Program's localization configuration for the assembly's Resources namespace.
        Services.Configure<Microsoft.Extensions.Localization.LocalizationOptions>(options => options.ResourcesPath = "");
        Services.Configure<RecipeFileStorageOptions>(options =>
        {
            options.Provider = "AzureBlob";
            options.AzureBlob.AccountName = "ownstorage123";
        });
    }

    [Theory]
    [InlineData("/images/demo/pasta.webp")]
    [InlineData("/uploads/recipe-images/photo.webp")]
    [InlineData("/api/recipe-images/photo.webp")]
    [InlineData("https://ownstorage123.blob.core.windows.net/recipe-images/photo.webp")]
    public void TrustedImage_LoadsAutomaticallyWithoutReferrer(string source)
    {
        var cut = RenderComponent<Image>(p => p.Add(x => x.Src, source));
        Assert.Equal(source, cut.Find("img").GetAttribute("src"));
        Assert.Equal("no-referrer", cut.Find("img").GetAttribute("referrerpolicy"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Theory]
    [InlineData("https://external.example/photo.jpg", "external.example")]
    [InlineData("https://otherstorage.blob.core.windows.net/photo.jpg", "otherstorage.blob.core.windows.net")]
    [InlineData("https://ownstorage123.blob.core.windows.net.attacker.example/photo.jpg", "ownstorage123.blob.core.windows.net.attacker.example")]
    [InlineData("https://ownstorage123.blob.core.windows.net:8443/photo.jpg", "ownstorage123.blob.core.windows.net")]
    public void UnreviewedHost_HasOnlyExplicitSafeLink(string source, string host)
    {
        var cut = RenderComponent<Image>(p => p.Add(x => x.Src, source));
        Assert.Empty(cut.FindAll("img, iframe, source"));
        var link = cut.Find("a");
        Assert.Equal(source, link.GetAttribute("href"));
        Assert.Contains(host, link.TextContent);
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        Assert.Equal("no-referrer", link.GetAttribute("referrerpolicy"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml,<svg/>")]
    [InlineData("http://external.example/photo.jpg")]
    [InlineData("//external.example/photo.jpg")]
    [InlineData("/\\external.example/photo.jpg")]
    [InlineData("/%2fexternal.example/photo.jpg")]
    [InlineData("/images/demo/../../redirect?url=https://external.example")]
    [InlineData("/images/demo/%2e%2e/%2e%2e/redirect")]
    [InlineData("https://ownstorage123.blob.core.windows.net@attacker.example/photo.jpg")]
    [InlineData("https://external.example/\nphoto.jpg")]
    public void UnsafeSource_NeverCreatesAutomaticRequestOrNavigation(string source)
    {
        var cut = RenderComponent<Image>(p => p.Add(x => x.Src, source));
        Assert.Empty(cut.FindAll("img, a, iframe, source"));
    }

    [Fact]
    public void AllEightDemoImages_RemainAutomatic()
    {
        for (var index = 1; index <= 8; index++)
        {
            var source = $"/images/demo/recipe-{index}.png";
            var cut = RenderComponent<Image>(p => p.Add(x => x.Src, source));
            Assert.Equal(source, cut.Find("img").GetAttribute("src"));
            Assert.Equal("no-referrer", cut.Find("img").GetAttribute("referrerpolicy"));
        }
    }

    [Fact]
    public void StorageAccountIsTrustedOnlyWhenConfiguredAsActiveProvider()
    {
        Services.Configure<RecipeFileStorageOptions>(options => options.Provider = "Local");
        var cut = RenderComponent<Image>(p => p.Add(x => x.Src, "https://ownstorage123.blob.core.windows.net/photo.jpg"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Single(cut.FindAll("a"));
    }

    [Fact]
    public void CardsAndRows_KeepExternalLinkOutsideRecipeNavigation()
    {
        var item = new RecipeOverviewItem { Id = Guid.NewGuid(), Title = "Recipe", ImageUrl = "https://external.example/photo.jpg" };
        var card = RenderComponent<RecipeCard>(p => p.Add(x => x.Item, item));
        var row = RenderComponent<RecipeListItem>(p => p.Add(x => x.Item, item));
        foreach (var cut in new IRenderedFragment[] { card, row })
        {
            Assert.Empty(cut.FindAll("img, a a"));
            Assert.Equal(2, cut.FindAll("a").Count);
            Assert.Single(cut.FindAll("a[target='_blank']"));
        }
    }

    [Fact]
    public void UploadPreview_UpdatesFromExternalLinkToLocalUploadedImage()
    {
        var cut = RenderComponent<PhotoUploadZone>(p => p.Add(x => x.ImageUrl, "https://external.example/photo.jpg"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Single(cut.FindAll("a[target='_blank']"));
        cut.SetParametersAndRender(p => p.Add(x => x.ImageUrl, "/api/recipe-images/uploaded.webp"));
        Assert.Equal("/api/recipe-images/uploaded.webp", cut.Find("img").GetAttribute("src"));
        Assert.Empty(cut.FindAll("a"));
    }
}
