using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeLibrary.Infrastructure.ContentModeration;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Web.Services;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class StartupSeedTests
{
    [Fact]
    public async Task Seeds_WaitForSchema_ThenCreateUserBeforeAssigningAdminRole()
    {
        await using var fixture = await SeedFixture.CreateAsync(development: true);
        var readiness = new PersistenceReadiness();
        using var warmup = fixture.StartWarmup(readiness, TimeSpan.FromSeconds(10));
        await warmup.StartAsync(default);
        await fixture.Schema.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(fixture.State.Operations);
        Assert.False(readiness.IsReady);

        fixture.Schema.Complete();
        await WaitUntilAsync(() => readiness.IsReady || readiness.HasPermanentlyFailed);
        Assert.True(readiness.IsReady);
        var operations = fixture.State.Operations.ToArray();
        Assert.True(Array.IndexOf(operations, "UserCreate") < Array.IndexOf(operations, "RoleLookup"));
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var user = await database.Users.SingleAsync(u => u.Email == "admin@example.invalid");
        var role = await database.Roles.SingleAsync(r => r.Name == "Admin");
        Assert.True(await database.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == role.Id));
        await warmup.StopAsync(default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deadline_ReachesActualSeedStore_AndStopsFurtherMutations(bool duringAdminSeed)
    {
        await using var fixture = await SeedFixture.CreateAsync(development: !duringAdminSeed);
        fixture.State.Block = true;
        fixture.Schema.Complete();
        var readiness = new PersistenceReadiness();
        using var warmup = fixture.StartWarmup(readiness, TimeSpan.FromMilliseconds(250));
        await warmup.StartAsync(default);
        await WaitUntilAsync(() => readiness.HasPermanentlyFailed);
        Assert.True(fixture.State.CancellationObserved);
        Assert.Equal(new[] { duringAdminSeed ? "RoleLookup" : "UserLookup" }, fixture.State.Operations);
        Assert.Equal(0, fixture.State.Mutations);
        await warmup.StopAsync(default);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }

    private sealed class SeedFixture(SqliteConnection connection, ServiceProvider services,
        Schema schema, StoreState state) : IAsyncDisposable
    {
        public ServiceProvider Services => services;
        public Schema Schema => schema;
        public StoreState State => state;
        public PersistenceWarmupHostedService StartWarmup(PersistenceReadiness readiness, TimeSpan timeout) =>
            new(services.GetRequiredService<IPersistenceInitializer>(),
                Options.Create(new PersistenceWarmupOptions { Timeout = timeout }), readiness,
                NullLogger<PersistenceWarmupHostedService>.Instance);

        public static async Task<SeedFixture> CreateAsync(bool development)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new SeedEnvironment { EnvironmentName = development ? "Development" : "Production" });
            services.AddDbContext<RecipeDbContext>(options => options.UseSqlite(connection));
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<RecipeDbContext>().AddDefaultTokenProviders();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContentModeration:AdminEmails:0"] = "admin@example.invalid"
            }).Build();
            services.AddContentModeration(configuration);
            services.Configure<IdentitySeedUserOptions>(options =>
            {
                options.Email = "admin@example.invalid";
                options.UserName = "seed-admin";
                options.Password = "TestPassword123!";
            });
            services.AddSingleton<IdentitySeedUserHostedService>();
            var schema = new Schema();
            var state = new StoreState();
            services.AddSingleton(state);
            services.AddSingleton<IPersistenceSchemaInitializer>(schema);
            services.AddSingleton<IPersistenceInitializer, PersistenceInitializer>();
            services.AddScoped<IUserStore<ApplicationUser>, ObservedUserStore>();
            services.AddScoped<IRoleStore<IdentityRole>, ObservedRoleStore>();
            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RecipeDbContext>().Database.EnsureCreatedAsync();
            return new SeedFixture(connection, provider, schema, state);
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class Schema : IPersistenceSchemaInitializer
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete() => _completion.TrySetResult();
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await _completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class StoreState
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Operations { get; } = new();
        public bool Block;
        public bool CancellationObserved;
        public int Mutations;
        public async Task ObserveAsync(string operation, CancellationToken cancellationToken)
        {
            Operations.Enqueue(operation);
            Assert.True(cancellationToken.CanBeCanceled);
            if (!Block) return;
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { CancellationObserved = true; throw; }
        }
    }

    private sealed class ObservedUserStore(RecipeDbContext context, StoreState state)
        : UserStore<ApplicationUser, IdentityRole, RecipeDbContext>(context)
    {
        public override async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            await state.ObserveAsync("UserLookup", cancellationToken);
            return await base.FindByEmailAsync(normalizedEmail, cancellationToken);
        }
        public override Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Operations.Enqueue("UserCreate");
            state.Mutations++;
            return base.CreateAsync(user, cancellationToken);
        }
    }

    private sealed class ObservedRoleStore(RecipeDbContext context, StoreState state)
        : RoleStore<IdentityRole, RecipeDbContext>(context)
    {
        public override async Task<IdentityRole?> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            await state.ObserveAsync("RoleLookup", cancellationToken);
            return await base.FindByNameAsync(normalizedName, cancellationToken);
        }
        public override Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Mutations++;
            return base.CreateAsync(role, cancellationToken);
        }
    }

    private sealed class SeedEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "RecipeLibrary";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
