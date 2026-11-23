using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(RecipeShareE2eCollection))]
public sealed class RecipeShareTests(E2eFixture fixture)
{
    [Fact]
    public async Task VisitorReadsSharedRecipeSignsInAndSavesAnIndependentCopy()
    {
        const string email = "recipe-recipient@example.invalid";
        const string password = "RecipientTestPass123";
        var recipient = new ApplicationUser
        {
            UserName = "recipe-recipient",
            NormalizedUserName = "RECIPE-RECIPIENT",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        recipient.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(recipient, password);

        var originalUrl = $"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId:D}";
        await using var ownerContext = await fixture.NewAuthenticatedContextAsync();
        await ownerContext.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = fixture.BaseUrl });
        var owner = await ownerContext.NewPageAsync();
        owner.UseBlazorDefaults();
        await owner.GotoAsync(originalUrl);

        var imageUrl = await UploadImageAsync(owner);
        await using (var db = fixture.CreateDbContext())
        {
            db.Users.Add(recipient);
            var recipe = await db.Recipes.SingleAsync(r => r.Id == fixture.Seed.RecipeId);
            recipe.ImageUrl = imageUrl;
            recipe.SourceUrl = "https://example.invalid/lasagna";
            await db.SaveChangesAsync();
        }

        await owner.ReloadAsync();
        await owner.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var rating = owner.GetByTestId(UiTestIds.RecipeRatingOption(5));
        await rating.ClickAsync();
        await Expect(rating).ToHaveAttributeAsync("aria-pressed", "true");
        await owner.GetByTestId(UiTestIds.RecipeShareEnable).ClickAsync();
        var link = await owner.GetByTestId(UiTestIds.RecipeShareLink).InputValueAsync();
        Assert.DoesNotContain(fixture.Seed.RecipeId.ToString(), link);
        await owner.GetByTestId(UiTestIds.RecipeShareCopyLink).ClickAsync();
        await Expect(owner.GetByRole(AriaRole.Status).Filter(new() { HasText = "Deellink gekopieerd." })).ToBeVisibleAsync();
        Assert.Equal(link, await owner.EvaluateAsync<string>("navigator.clipboard.readText()"));

        await using var visitorContext = await fixture.Browser.NewContextAsync(new() { Locale = "nl-NL" });
        var visitor = await visitorContext.NewPageAsync();
        visitor.UseBlazorDefaults();
        var publicResponse = await visitor.GotoAsync(link);
        Assert.Equal(200, publicResponse!.Status);
        Assert.Contains("no-store", publicResponse.Headers["cache-control"]);
        await Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = TestDataSeeder.LasagnaTitle })).ToBeVisibleAsync();
        await Expect(visitor.GetByText("Mix ingredients.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(visitor.GetByText("Tomaten", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(visitor.GetByTestId(UiTestIds.RecipeRating)).ToHaveCountAsync(0);
        await Expect(visitor.GetByTestId(UiTestIds.RecipeEdit)).ToHaveCountAsync(0);
        await Expect(visitor.GetByTestId(UiTestIds.SharedRecipeSave)).ToHaveCountAsync(0);
        var preview = visitor.GetByRole(AriaRole.Img, new() { Name = TestDataSeeder.LasagnaTitle });
        var sharedImageUrl = (await preview.GetAttributeAsync("src"))!;
        var publicImage = await visitorContext.APIRequest.GetAsync(fixture.BaseUrl + sharedImageUrl);
        Assert.Equal(200, publicImage.Status);
        Assert.Equal("image/png", publicImage.Headers["content-type"]);

        await Expect(visitor.GetByTestId(UiTestIds.SharedRecipeSignIn)).ToHaveTextAsync("Log in om een kopie op te slaan");
        var languageSwitcher = visitor.GetByTestId(UiTestIds.LanguageSwitcher);
        var englishOption = visitor.GetByTestId(UiTestIds.LanguageEn);
        await languageSwitcher.ClickAsync();
        await Expect(englishOption).ToBeVisibleAsync();
        await visitor.Mouse.ClickAsync(4, 4);
        await Expect(englishOption).ToBeHiddenAsync();
        await languageSwitcher.PressAsync("Enter");
        await englishOption.ClickAsync();
        await Expect(visitor).ToHaveURLAsync(link);
        await Expect(visitor.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await Expect(visitor.GetByTestId(UiTestIds.SharedRecipeSignIn)).ToHaveTextAsync("Sign in to save a copy");
        await Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = "Ingredients", Exact = true })).ToBeVisibleAsync();
        var flagPath = await languageSwitcher.Locator("img").GetAttributeAsync("src");
        var publicFlag = await visitorContext.APIRequest.GetAsync(fixture.BaseUrl + flagPath);
        Assert.Equal(200, publicFlag.Status);
        Assert.Equal("image/svg+xml", publicFlag.Headers["content-type"]);

        await visitor.GetByTestId(UiTestIds.SharedRecipeSignIn).ClickAsync();
        await Expect(visitor.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await visitor.Locator("#login-email").FillAsync(email);
        await visitor.Locator("#login-password").FillAsync(password);
        await visitor.Locator("button[type=submit]").ClickAsync();
        await Expect(visitor).ToHaveURLAsync(link);

        var saveButton = visitor.GetByTestId(UiTestIds.SharedRecipeSave);
        await Expect(saveButton).ToHaveTextAsync("Save a copy");
        await languageSwitcher.ClickAsync();
        await visitor.GetByTestId(UiTestIds.LanguageNl).ClickAsync();
        await Expect(visitor).ToHaveURLAsync(link);
        await Expect(visitor.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        await Expect(saveButton).ToHaveTextAsync("Sla een kopie op");
        var savePath = await visitor.Locator("form").GetAttributeAsync("action");
        var unprotectedPost = await visitorContext.APIRequest.PostAsync(fixture.BaseUrl + savePath);
        Assert.Equal(400, unprotectedPost.Status);

        await saveButton.ClickAsync();
        await Expect(visitor).ToHaveURLAsync(new Regex("/recipes/[0-9a-f-]{36}$"));
        var copyUrl = visitor.Url;
        Assert.NotEqual(originalUrl, copyUrl);
        var copyId = Guid.Parse(new Uri(copyUrl).Segments[^1]);
        await Expect(visitor.GetByTestId(UiTestIds.RecipeRatingOption(0))).ToHaveAttributeAsync("aria-pressed", "true");
        var personalImage = await visitorContext.APIRequest.GetAsync(fixture.BaseUrl + imageUrl);
        Assert.Equal(200, personalImage.Status);
        var originalApi = await visitorContext.APIRequest.GetAsync($"{fixture.BaseUrl}/api/v1/recipes/{fixture.Seed.RecipeId:D}");
        Assert.Equal(404, originalApi.Status);

        await visitor.GotoAsync(link);
        await saveButton.ClickAsync();
        await Expect(visitor).ToHaveURLAsync(copyUrl);
        await visitor.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await visitor.GetByTestId(UiTestIds.RecipeEdit).ClickAsync();
        await visitor.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync("Mijn lasagne");
        await visitor.SaveRecipeAndWaitForDetailAsync();

        await owner.ReloadAsync();
        await Expect(owner.GetByRole(AriaRole.Heading, new() { Name = TestDataSeeder.LasagnaTitle })).ToBeVisibleAsync();
        await Expect(owner.GetByTestId(UiTestIds.RecipeShareLink)).ToHaveValueAsync(link);
        await owner.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await owner.GetByTestId(UiTestIds.RecipeShareRevoke).ClickAsync();
        await owner.GetByTestId(UiTestIds.RecipeShareEnable).ClickAsync();
        var renewedLink = await owner.GetByTestId(UiTestIds.RecipeShareLink).InputValueAsync();
        Assert.NotEqual(link, renewedLink);
        Assert.Equal(404, (await visitor.GotoAsync(link))!.Status);
        Assert.Equal(404, (await visitorContext.APIRequest.GetAsync(fixture.BaseUrl + sharedImageUrl)).Status);
        await visitor.GotoAsync(copyUrl);
        await Expect(visitor.GetByRole(AriaRole.Heading, new() { Name = "Mijn lasagne" })).ToBeVisibleAsync();

        await using var verificationDb = fixture.CreateDbContext();
        var copy = await verificationDb.Recipes.Include(r => r.Ingredients).Include(r => r.InstructionSteps)
            .SingleAsync(r => r.OwnerUserId == recipient.Id);
        Assert.Equal(copyId, copy.Id);
        Assert.Null(copy.HouseholdId);
        Assert.Null(copy.PublicShareKey);
        Assert.Equal("https://example.invalid/lasagna", copy.SourceUrl);
        Assert.Equal(2, copy.Ingredients.Count);
        Assert.Equal("Mix ingredients.", Assert.Single(copy.InstructionSteps).Text);
    }

    private static Task<string> UploadImageAsync(IPage page) => page.EvaluateAsync<string>("""
        async () => {
            const data = new FormData();
            const png = await (await fetch("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")).blob();
            data.append("file", png, "test.png");
            const response = await fetch("/api/upload-recipe-image", { method: "POST", body: data });
            if (!response.ok) throw new Error(await response.text());
            return (await response.json()).url;
        }
        """);
}

[CollectionDefinition(nameof(RecipeShareE2eCollection))]
public sealed class RecipeShareE2eCollection : ICollectionFixture<E2eFixture>;
