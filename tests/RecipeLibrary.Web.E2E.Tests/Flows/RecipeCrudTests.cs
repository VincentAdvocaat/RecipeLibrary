using Microsoft.Playwright;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(RecipeCrudE2eCollection))]
public sealed class RecipeCrudTests(E2eFixture fixture)
{
    [Fact]
    public async Task CreateMinimalRecipe_RedirectsToDetail()
    {
        var title = $"E2E Recipe {Guid.NewGuid():N}";
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync(title);
        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillBlazorInputAsync("Gehakt");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Mix and bake.");
        await page.SaveRecipeAndWaitForDetailAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = title })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreateWithPreparation_ShowsOnDetail()
    {
        var title = $"E2E Prep {Guid.NewGuid():N}";
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync(title);
        await page.FillIngredientRowAsync(0, "Gehakt", "ruim");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Step one.");
        await page.SaveRecipeAndWaitForDetailAsync();

        await Assertions.Expect(page.GetByText("ruim")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Create_EmptyTitle_ShowsValidation()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillBlazorInputAsync("Gehakt");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Step.");
        await page.GetByTestId(UiTestIds.RecipeSave).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/recipes/create"));
    }

    [Fact]
    public async Task EditRecipe_UpdatesTitle()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");

        await page.GetByTestId(UiTestIds.RecipeEdit).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeTitle).WaitForAsync();
        var newTitle = $"Updated {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync(newTitle);
        await page.SaveRecipeAndWaitForDetailAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = newTitle })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task DeleteRecipe_ReturnsToOverview()
    {
        var title = $"Delete Me {Guid.NewGuid():N}";
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync(title);
        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillBlazorInputAsync("Gehakt");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Step.");
        await page.SaveRecipeAndWaitForDetailAsync();

        await page.GetByTestId(UiTestIds.RecipeDelete).ClickAsync();
        await page.GetByTestId(UiTestIds.DeleteConfirmYes).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/recipes/?$"));
        await Assertions.Expect(page.GetByText(title)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task IngredientAutocomplete_SelectsSuggestion()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillAsync("ge");
        await page.GetByRole(AriaRole.Button, new() { Name = "Gehakt" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Gehakt" }).ClickAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.IngredientRowName(0))).ToHaveValueAsync("Gehakt");
    }

    [Fact]
    public async Task AddAndRemoveIngredientRow_WorksOnCreatePage()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.IngredientAddRow).ClickAsync();
        await page.GetByTestId(UiTestIds.IngredientRowName(1)).WaitForAsync();

        await page.GetByTestId(UiTestIds.IngredientRowRemove(1)).ClickAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.IngredientRowName(1))).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AddIngredientTag_ShowsChip()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.TagInput).FillBlazorInputAsync("weekmenu");
        await page.GetByTestId(UiTestIds.TagAdd).ClickAsync();
        await Assertions.Expect(page.GetByText("weekmenu")).ToBeVisibleAsync();
    }
}
