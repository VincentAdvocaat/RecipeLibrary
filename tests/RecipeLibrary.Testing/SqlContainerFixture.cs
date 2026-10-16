using Testcontainers.MsSql;
using Xunit;

namespace RecipeLibrary.Testing;

public sealed class SqlContainerFixture : IAsyncLifetime
{
    private readonly TestSqlDatabase _container = new();

    public string ConnectionString { get; private set; } = string.Empty;

    public TestWebApplicationFactory Factory { get; private set; } = null!;

    public TestSeedData Seed { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.ConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__RecipeDb", ConnectionString);
        Factory = new TestWebApplicationFactory(ConnectionString);
        Seed = await Factory.SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
