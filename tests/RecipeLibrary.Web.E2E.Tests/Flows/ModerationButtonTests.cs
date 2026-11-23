using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[CollectionDefinition(nameof(ModerationE2eCollection))]
public sealed class ModerationE2eCollection : ICollectionFixture<E2eFixture>;

[Collection(nameof(ModerationE2eCollection))]
public sealed class ModerationButtonTests(E2eFixture fixture)
{
    [Fact]
    public async Task AdminCanApproveAndRejectPendingRecipesAndReports()
    {
        await using var db = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var role = new IdentityRole(ContentModerationOptions.AdminRoleName)
        {
            NormalizedName = ContentModerationOptions.AdminRoleName.ToUpperInvariant(),
        };
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<string> { RoleId = role.Id, UserId = TestDataSeeder.TestOwnerUserId });

        var cases = new[]
        {
            (Title: "Keur recept goed", Report: false, Decision: ModerationStatus.Approved),
            (Title: "Wijs recept af", Report: false, Decision: ModerationStatus.Rejected),
            (Title: "Keur melding goed", Report: true, Decision: ModerationStatus.Approved),
            (Title: "Wijs melding af", Report: true, Decision: ModerationStatus.Rejected),
        };
        foreach (var item in cases)
        {
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), Title = new RecipeTitle(item.Title), OwnerUserId = TestDataSeeder.TestOwnerUserId,
                CreatedAt = now, UpdatedAt = now, Servings = 2,
                ModerationStatus = item.Report ? ModerationStatus.Approved : ModerationStatus.NeedsReview,
            };
            db.Recipes.Add(recipe);
            if (item.Report)
                db.ContentReports.Add(new ContentReport
                {
                    Id = Guid.NewGuid(), RecipeId = recipe.Id, ReporterUserId = TestDataSeeder.TestOwnerUserId, CreatedAt = now,
                });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync(fixture.BaseUrl + "/Account/Login");
        await page.Locator("#login-email").FillAsync("e2e@example.invalid");
        await page.Locator("#login-password").FillAsync("E2eTestPass123");
        await page.Locator("button[type=submit]").ClickAsync();
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();
        var response = await page.GotoAsync(fixture.BaseUrl + "/admin/moderation");
        Assert.True(response?.Ok == true, $"Moderation page returned {response?.Status}. {fixture.Diagnostics}");
        await Expect(page).ToHaveURLAsync(new Regex("/admin/moderation$"));
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await Expect(page.GetByTestId(UiTestIds.AddRecipeFab)).ToHaveCountAsync(0);

        foreach (var item in cases)
        {
            var row = page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = item.Title });
            var button = item.Decision == ModerationStatus.Approved ? "Goedkeuren" : "Afwijzen";
            await row.GetByRole(AriaRole.Button, new() { Name = button }).ClickAsync();
            await Expect(row).ToHaveCountAsync(0);

            var recipes = await db.Recipes.AsNoTracking().ToListAsync();
            var recipe = Assert.Single(recipes, r => r.Title.Value == item.Title);
            Assert.Equal(item.Decision, recipe.ModerationStatus);
            if (item.Report)
                Assert.True(await db.ContentReports.Where(r => r.RecipeId == recipe.Id).Select(r => r.Handled).SingleAsync());
        }
    }
}
