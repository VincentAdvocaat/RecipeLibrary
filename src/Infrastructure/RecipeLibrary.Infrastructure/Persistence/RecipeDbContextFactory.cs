using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecipeLibrary.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core tools (migrations).
/// This keeps migrations creation independent from the Web project's runtime configuration.
/// </summary>
public sealed class RecipeDbContextFactory : IDesignTimeDbContextFactory<RecipeDbContext>
{
    public RecipeDbContext CreateDbContext(string[] args)
    {
        // Prefer a real connection string if provided (e.g. Azure SQL with Entra auth),
        // otherwise fall back to a local placeholder. Migrations generation does not require
        // a reachable database.
        var cs =
            Environment.GetEnvironmentVariable("ConnectionStrings__RecipeDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:RecipeDb")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=RecipeLibrary.Migrations;Trusted_Connection=True;TrustServerCertificate=True;";

        var builder = new DbContextOptionsBuilder<RecipeDbContext>();
        builder.UseSqlServer(cs, sql => sql.EnableRetryOnFailure());

        return new RecipeDbContext(builder.Options);
    }
}

