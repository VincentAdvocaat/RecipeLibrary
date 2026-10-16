using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace RecipeLibrary.Testing;

/// <summary>Uses Docker by default; an explicitly configured SQL Server supports hosts without Docker.</summary>
public sealed class TestSqlDatabase : IAsyncDisposable
{
    private MsSqlContainer? container;
    private string? databaseName;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var external = Environment.GetEnvironmentVariable("RECIPELIBRARY_TEST_SQL");
        if (string.IsNullOrWhiteSpace(external))
        {
            container = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
            return;
        }
        databaseName = "RecipeLibraryTests_" + Guid.NewGuid().ToString("N");
        ConnectionString = new SqlConnectionStringBuilder(external) { InitialCatalog = databaseName }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null) { await container.DisposeAsync(); return; }
        if (databaseName is null) return;
        var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // This identifier is generated above and cannot contain caller-controlled SQL.
        command.CommandText = $"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}
