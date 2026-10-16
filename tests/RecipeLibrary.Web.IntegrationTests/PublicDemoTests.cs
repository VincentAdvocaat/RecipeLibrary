using System.Net;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.Demo;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class PublicDemoTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task AnonymousStart_UsesCookie_SeedsEightRecipes_ResumesWithoutExtending()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var anonymous = await client.GetAsync("/recipes");
        Assert.Contains("/Account/Login", anonymous.Headers.Location?.ToString());
        var (owner, _) = await StartAsync(factory, client);
        var before = await SessionAsync(factory, owner);
        var page = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("demo-banner", await page.Content.ReadAsStringAsync());
        await CheckAsync(factory, async db =>
        {
            Assert.Equal(8, await db.Recipes.CountAsync(x => x.OwnerUserId == owner));
            Assert.Equal(3, await db.PantryItems.CountAsync(x => x.OwnerUserId == owner));
            Assert.Single(await db.ShoppingListGroups.Where(x => x.OwnerUserId == owner).ToListAsync());
        });
        var resume = await client.GetAsync("/demo");
        Assert.Equal("/recipes", resume.Headers.Location?.AbsolutePath);
        Assert.Equal(before.ExpiresAt, (await SessionAsync(factory, owner)).ExpiresAt);
    }

    [Fact]
    public async Task DemoScripts_ArePublicBeforeAuthentication()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var response = await client.GetAsync("/demo.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StartResetEnd_RequireAntiforgery()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/demo/start", new FormUrlEncodedContent([]))).StatusCode);
        await StartAsync(factory, client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/demo/reset", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/demo/end", new FormUrlEncodedContent([]))).StatusCode);
    }

    [Theory]
    [InlineData("/recipes/import")]
    [InlineData("/recipes/import-url")]
    [InlineData("/recipes/import-image")]
    [InlineData("/api/upload-recipe-image")]
    [InlineData("/ingredients/00000000-0000-0000-0000-000000000001/tags")]
    public async Task RestrictedHttpEndpoints_RejectBeforeParsingRequest(string path)
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        await StartAsync(factory, client);
        var response = await client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Demo.AccountRequired", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DirectHandlers_AreGuarded_AndUnknownIngredientsDoNotChangeCatalog()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        var catalogBefore = await CountCatalogAsync(factory);
        await AsDemoAsync(factory, owner, async services =>
        {
            var handler = services.GetRequiredService<ICommandHandler<CreateRecipeCommand, CreateRecipeResult>>();
            var recipe = await handler.HandleAsync(Recipe("Unbekend demo-ingrediënt " + Guid.NewGuid().ToString("N")));
            var db = services.GetRequiredService<RecipeDbContext>();
            Assert.Null((await db.RecipeIngredients.SingleAsync(x => x.RecipeId == recipe.RecipeId)).IngredientId);
            var import = services.GetRequiredService<IQueryHandler<ImportRecipeContentQuery, ImportRecipeResult>>();
            await Assert.ThrowsAsync<DemoAccessException>(() => import.HandleAsync(new ImportRecipeContentQuery()));
            var upload = services.GetRequiredService<ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>>();
            await Assert.ThrowsAsync<DemoAccessException>(() => upload.HandleAsync(new UploadRecipeImageCommand { FileName = "photo.png", ContentType = "image/png", Content = Stream.Null }));
            var tags = services.GetRequiredService<ICommandHandler<AddIngredientTagsCommand, AddIngredientTagsResult>>();
            await Assert.ThrowsAsync<DemoAccessException>(() => tags.HandleAsync(new AddIngredientTagsCommand()));
        });
        Assert.Equal(catalogBefore, await CountCatalogAsync(factory));
    }

    [Fact]
    public async Task SeparateSessions_CannotReadOrMutateEachOthersContent()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var a = Client(factory);
        using var b = Client(factory);
        var (first, _) = await StartAsync(factory, a);
        var (second, _) = await StartAsync(factory, b);
        Guid recipeId = default, groupId = default;
        await CheckAsync(factory, async db =>
        {
            recipeId = await db.Recipes.Where(x => x.OwnerUserId == first).Select(x => x.Id).FirstAsync();
            groupId = await db.ShoppingListGroups.Where(x => x.OwnerUserId == first).Select(x => x.Id).SingleAsync();
        });
        await AsDemoAsync(factory, second, async sp =>
        {
            Assert.Null(await sp.GetRequiredService<IQueryBus>().QueryAsync<GetRecipeByIdQuery, GetRecipeByIdResult?>(new() { RecipeId = recipeId }));
            Assert.False((await sp.GetRequiredService<ICommandBus>().SendAsync<DeleteRecipeCommand, DeleteRecipeResult>(new() { RecipeId = recipeId })).Deleted);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sp.GetRequiredService<IQueryBus>()
                .QueryAsync<GetPantryItemsQuery, GetPantryItemsResult>(new() { ShoppingListGroupId = groupId }));
            await Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
                .SendAsync<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>(new() { OwnerUserId = first, DefaultListNameFormat = "List {0}" }));
        });
        await CheckAsync(factory, async db => Assert.True(await db.Recipes.AnyAsync(x => x.Id == recipeId)));
    }

    [Fact]
    public async Task Reset_PreservesExpiryAndAttemptCount_InvalidatesOldCircuit()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await AsDemoAsync(factory, owner, sp => sp.GetRequiredService<ICommandBus>().SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Demo ingredient")));
        var before = await SessionAsync(factory, owner);
        var response = await PostFormAsync(client, "/demo/reset");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await SessionAsync(factory, owner);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Equal(before.SaveAttempts, after.SaveAttempts);
        Assert.Equal(2, after.Revision);
        await AsDemoAsync(factory, owner, sp => Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
            .SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Stale"))));
        await CheckAsync(factory, async db => Assert.Equal(8, await db.Recipes.CountAsync(x => x.OwnerUserId == owner)));
    }

    [Fact]
    public async Task End_DeletesOwnContentAndCookie()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await AsDemoAsync(factory, owner, sp => sp.GetRequiredService<ICommandBus>().SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Before end")));
        var end = await PostFormAsync(client, "/demo/end");
        Assert.Equal("/demo?state=ended", end.Headers.Location?.ToString());
        await CheckAsync(factory, async db =>
        {
            Assert.False(await db.Users.AnyAsync(x => x.Id == owner));
            Assert.False(await db.DemoSaveAttempts.AnyAsync(x => x.UserId == owner));
            Assert.False(await db.Recipes.AnyAsync(x => x.OwnerUserId == owner));
            Assert.False(await db.PantryItems.AnyAsync(x => x.OwnerUserId == owner));
            Assert.False(await db.ShoppingListGroups.AnyAsync(x => x.OwnerUserId == owner));
        });
        await AsDemoAsync(factory, owner, sp => Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
            .SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Ended"))));
    }

    [Fact]
    public async Task ExpiredCircuitAndDisabledDemo_AreDenied_CleanupPreservesNormalUser()
    {
        var clock = new ManualClock();
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString, clock);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await CheckAsync(factory, async db =>
        {
            var recipe = await db.Recipes.FirstAsync(x => x.OwnerUserId == owner);
            db.ContentModerationEvents.Add(new() { Id = Guid.NewGuid(), DemoOwnerUserId = owner, RecipeId = recipe.Id, CategoriesSummary = "test" });
            await db.SaveChangesAsync();
        });
        clock.Advance(TimeSpan.FromMinutes(61));
        await AsDemoAsync(factory, owner, sp => Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
            .SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Expired"))));
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoSessionService>().CleanupAsync(default);
            await scope.ServiceProvider.GetRequiredService<DemoSessionService>().CleanupAsync(default);
        }
        await CheckAsync(factory, async db =>
        {
            Assert.False(await db.Users.AnyAsync(x => x.Id == owner));
            Assert.False(await db.ContentModerationEvents.AnyAsync(x => x.DemoOwnerUserId == owner));
            Assert.True(await db.Recipes.AnyAsync(x => x.OwnerUserId == TestDataSeeder.TestOwnerUserId));
        });
    }

    [Fact]
    public async Task ConcurrentSaves_KeepDurableAttemptLimit_AfterReset()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString, configure: o => o.MaxSaveAttempts = 1);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        async Task<bool> Save()
        {
            try
            {
                await AsDemoAsync(factory, owner, sp => sp.GetRequiredService<ICommandBus>().SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Concurrent")));
                return true;
            }
            catch (DemoAccessException) { return false; }
        }
        var results = await Task.WhenAll(Save(), Save());
        Assert.Single(results, x => x);
        Assert.Equal(1, (await SessionAsync(factory, owner)).SaveAttempts);
        await PostFormAsync(client, "/demo/reset");
        await AsDemoAsync(factory, owner, sp => Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
            .SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("After reset"))), 2);
    }

    [Fact]
    public async Task ImageAndEntityLimits_RejectAndRollBack()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString, configure: o => o.MaxRecipes = 8);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await AsDemoAsync(factory, owner, async sp =>
        {
            var bus = sp.GetRequiredService<ICommandBus>();
            await Assert.ThrowsAsync<DemoAccessException>(() => bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Too many")));
            var group = await sp.GetRequiredService<RecipeDbContext>().ShoppingListGroups.SingleAsync(x => x.OwnerUserId == owner);
            var list = await sp.GetRequiredService<RecipeDbContext>().ShoppingLists.SingleAsync(x => x.GroupId == group.Id);
            for (var i = 0; i < 100; i++)
            {
                try { await bus.SendAsync<AddManualShoppingListItemCommand, AddManualShoppingListItemResult>(new() { ShoppingListId = list.Id, DisplayName = "Item " + i }); }
                catch (DemoAccessException) { break; }
            }
            Assert.Equal(100, await sp.GetRequiredService<RecipeDbContext>().ShoppingListItems.CountAsync(x => x.ShoppingListId == list.Id));
        });
    }

    [Fact]
    public async Task CapacityAndAddressQuotas_AreEnforcedAcrossConcurrentStarts()
    {
        var address = Guid.NewGuid().ToString();
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString, configure: o => o.StartsPerIpPerHour = 1);
        async Task<bool> Start()
        {
            using var scope = factory.Services.CreateScope();
            try { await scope.ServiceProvider.GetRequiredService<DemoSessionService>().StartAsync(address, default); return true; }
            catch (DemoAccessException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Start(), Start()), x => x);
        var settings = factory.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
        settings.MaxActiveSessions = 1;
        using var scope = factory.Services.CreateScope();
        await Assert.ThrowsAsync<DemoAccessException>(() => scope.ServiceProvider.GetRequiredService<DemoSessionService>()
            .StartAsync(Guid.NewGuid().ToString(), default));
    }

    [Fact]
    public async Task FailedValidationConsumesAttempt_AndImagesAreRestricted()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await AsDemoAsync(factory, owner, async sp =>
        {
            var bus = sp.GetRequiredService<ICommandBus>();
            await Assert.ThrowsAsync<ArgumentException>(() => bus.SendAsync<CreateRecipeCommand, CreateRecipeResult>(new()
            {
                Title = "", Ingredients = [new() { Name = "Wortel", Quantity = 1, Unit = "Gram" }],
                InstructionSteps = [new() { StepNumber = 1, Text = "Cook" }]
            }));
            var recipe = await sp.GetRequiredService<RecipeDbContext>().Recipes.AsNoTracking()
                .Include(x => x.Ingredients).Include(x => x.InstructionSteps).FirstAsync(x => x.OwnerUserId == owner);
            UpdateRecipeCommand Update(string image) => new()
            {
                RecipeId = recipe.Id, Title = recipe.Title.Value, ImageUrl = image,
                Ingredients = recipe.Ingredients.Select(x => new CreateRecipeIngredientDto { Name = x.Name, Quantity = x.Quantity?.Value, Unit = x.Unit?.ToString() }).ToList(),
                InstructionSteps = recipe.InstructionSteps.Select(x => new CreateRecipeInstructionStepDto { StepNumber = x.StepNumber, Text = x.Text }).ToList()
            };
            await bus.SendAsync<UpdateRecipeCommand, UpdateRecipeResult>(Update(recipe.ImageUrl!));
            await Assert.ThrowsAsync<DemoAccessException>(() => bus.SendAsync<UpdateRecipeCommand, UpdateRecipeResult>(Update("https://example.com/image.jpg")));
        });
        Assert.Equal(2, (await SessionAsync(factory, owner)).SaveAttempts);
    }

    [Fact]
    public async Task DisabledSessionsAndResetLimit_AreEnforced()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString, configure: o => o.MaxResets = 0);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        var reset = await PostFormAsync(client, "/demo/reset");
        Assert.Equal("/demo?state=limit", reset.Headers.Location?.ToString());
        Assert.Equal(1, (await SessionAsync(factory, owner)).Revision);
        factory.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Enabled = false;
        await AsDemoAsync(factory, owner, sp => Assert.ThrowsAsync<DemoAccessException>(() => sp.GetRequiredService<ICommandBus>()
            .SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Disabled"))));
        var disabled = await client.GetAsync("/demo");
        Assert.Contains("momenteel niet beschikbaar", await disabled.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RapidSearchesInOneCircuit_AreSerialized()
    {
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString);
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await AsDemoAsync(factory, owner, async sp =>
        {
            var bus = sp.GetRequiredService<IQueryBus>();
            await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => bus.QueryAsync<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>(
                new() { Query = "tom", CultureName = "nl-NL" })));
        });
    }

    [Fact]
    public async Task FailedSeed_RollsBackNewIdentity_AndResetRestoresOriginalContent()
    {
        var fault = new SeedFault();
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString,
            customize: services => services.AddDbContext<RecipeDbContext>(options => options.AddInterceptors(fault)));
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        Guid[] original = [];
        int usersBefore = 0, sessionsBefore = 0;
        await CheckAsync(factory, async db =>
        {
            original = await db.Recipes.Where(x => x.OwnerUserId == owner).Select(x => x.Id).ToArrayAsync();
            usersBefore = await db.Users.CountAsync();
            sessionsBefore = await db.DemoSessions.CountAsync();
        });
        fault.Enabled = true;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<DemoSessionService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ResetAsync(owner, 1, default));
            await Assert.ThrowsAsync<DbUpdateException>(() => service.StartAsync(Guid.NewGuid().ToString(), default));
        }
        await CheckAsync(factory, async db =>
        {
            Assert.Equal(usersBefore, await db.Users.CountAsync());
            Assert.Equal(sessionsBefore, await db.DemoSessions.CountAsync());
            Assert.Equal(original.Order(), (await db.Recipes.Where(x => x.OwnerUserId == owner).Select(x => x.Id).ToArrayAsync()).Order());
            Assert.Equal(1, (await db.DemoSessions.SingleAsync(x => x.UserId == owner)).Revision);
        });
    }

    private sealed class SeedFault : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<Recipe>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Injected demo seed failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(30, false, false)]
    [InlineData(1, true, false)]
    [InlineData(1, true, true)]
    public async Task RecipeSaveRetry_UsesOneReservation_EvenForLastAttempt(int limit, bool failAfterCommit, bool failReservationCommit)
    {
        var saveFault = new RetrySaveFault();
        var commitFault = new RetryCommitFault();
        await using var factory = new DemoWebApplicationFactory(fixture.ConnectionString,
            configure: options => options.MaxSaveAttempts = limit,
            customize: services => services.AddDbContext<RecipeDbContext>(options => options
                .AddInterceptors(saveFault, commitFault).ReplaceService<IExecutionStrategyFactory, RetryStrategyFactory>()));
        using var client = Client(factory);
        var (owner, _) = await StartAsync(factory, client);
        await CheckAsync(factory, async db =>
        {
            var session = await db.DemoSessions.SingleAsync(x => x.UserId == owner);
            session.SaveAttempts = limit - 1;
            await db.SaveChangesAsync();
        });
        saveFault.Enabled = !failAfterCommit;
        commitFault.Enabled = failAfterCommit;
        commitFault.ReservationCommit = failReservationCommit;
        Guid resultId = default;
        await AsDemoAsync(factory, owner, async sp =>
        {
            var result = await sp.GetRequiredService<ICommandBus>().SendAsync<CreateRecipeCommand, CreateRecipeResult>(Recipe("Retry ingredient"));
            resultId = result.RecipeId;
        });
        Assert.Equal(1, saveFault.Hits + commitFault.Hits);
        await CheckAsync(factory, async db =>
        {
            Assert.Equal(limit, (await db.DemoSessions.SingleAsync(x => x.UserId == owner)).SaveAttempts);
            Assert.Equal(9, await db.Recipes.CountAsync(x => x.OwnerUserId == owner));
            var reservation = await db.DemoSaveAttempts.SingleAsync(x => x.UserId == owner);
            Assert.Equal(resultId, reservation.CompletedRecipeId);
            Assert.True(await db.Recipes.AnyAsync(x => x.Id == resultId));
        });
    }

    private sealed class RetryFaultException : Exception;

    private sealed class RetrySaveFault : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public int Hits;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<Recipe>().Any(x => x.State == EntityState.Added)
                && Interlocked.CompareExchange(ref Hits, 1, 0) == 0)
                throw new RetryFaultException();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class RetryCommitFault : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public bool ReservationCommit { get; set; }
        public int Hits;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var hasRecipe = eventData.Context!.ChangeTracker.Entries<Recipe>().Any();
            var reservation = eventData.Context.ChangeTracker.Entries<DemoSaveAttempt>().Any() && !hasRecipe;
            if (Enabled && (ReservationCommit ? reservation : hasRecipe)
                && Interlocked.CompareExchange(ref Hits, 1, 0) == 0)
                throw new RetryFaultException();
            return Task.CompletedTask;
        }
    }

    public sealed class RetryStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception.GetBaseException() is RetryFaultException;
    }

    public static CreateRecipeCommand Recipe(string ingredient) => new()
    {
        Title = "Handmatig demorecept", Servings = 2,
        Ingredients = [new() { Name = ingredient, Quantity = 1, Unit = "Gram" }],
        InstructionSteps = [new() { StepNumber = 1, Text = "Meng de ingrediënten en serveer." }]
    };

    public static HttpClient Client(DemoWebApplicationFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    public static async Task<(string Owner, HttpResponseMessage Response)> StartAsync(DemoWebApplicationFactory factory, HttpClient client)
    {
        var response = await PostFormAsync(client, "/demo/start");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/recipes", response.Headers.Location?.ToString());
        var header = response.Headers.GetValues("Set-Cookie").First(x => x.StartsWith(".AspNetCore.Identity.Application="));
        var cookie = header.Split(';')[0].Split('=', 2)[1];
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookie));
        return (ticket!.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!, response);
    }

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync("/demo?state=ended");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        return await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
    }

    private static async Task AsDemoAsync(DemoWebApplicationFactory factory, string owner, Func<IServiceProvider, Task> action, int revision = 1)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DemoIdentity>().SetCircuitUser(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, owner), new Claim(DemoIdentity.DemoClaim, "true"),
            new Claim(DemoIdentity.RevisionClaim, revision.ToString())], "demo-test")));
        await action(scope.ServiceProvider);
    }

    private static async Task CheckAsync(DemoWebApplicationFactory factory, Func<RecipeDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<RecipeDbContext>());
    }

    private static async Task<int> CountCatalogAsync(DemoWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        return await db.Ingredients.CountAsync();
    }

    private static async Task<DemoSession> SessionAsync(DemoWebApplicationFactory factory, string owner)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RecipeDbContext>().DemoSessions.AsNoTracking().SingleAsync(x => x.UserId == owner);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
