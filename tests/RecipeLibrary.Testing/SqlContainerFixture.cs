using Testcontainers.MsSql;
using Xunit;

namespace RecipeLibrary.Testing;

public sealed class SqlContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public TestWebApplicationFactory Factory { get; private set; } = null!;

    public TestSeedData Seed { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
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
