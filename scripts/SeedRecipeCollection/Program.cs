using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Infrastructure.ContentModeration;
using RecipeLibrary.Infrastructure.Persistence;

const string usage = """
    SeedRecipeCollection validate <seed-file>
    SeedRecipeCollection apply <seed-file> <owner-id>

    Run apply with ConnectionStrings__RecipeDb set to the target database. Apply
    the AddRecipeRatings migration first and use the ID of an existing account.
    """;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine(usage);
    return 0;
}

var apply = args is ["apply", _, _] && !string.IsNullOrWhiteSpace(args[2]);
if (!apply && args is not ["validate", _])
{
    Console.Error.WriteLine(usage);
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
var ct = cancellation.Token;

try
{
    await using var file = File.OpenRead(args[1]);
    var collection = await JsonSerializer.DeserializeAsync<SeedCollection>(
        file, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
        ?? throw new InvalidDataException("The seed file is empty.");
    var ready = collection.Validate();
    Console.WriteLine($"Ready: {ready.Length}, with a rating: {ready.Count(entry => entry.Source.RatingStars.HasValue)}, skipped: {collection.Recipes.Length - ready.Length}.");
    if (!apply)
    {
        return 0;
    }

    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var connectionString = configuration.GetConnectionString("RecipeDb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Set ConnectionStrings__RecipeDb to the target database connection string.");
    }

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddPersistence(connectionString);
    services.AddContentModeration(configuration);
    services.AddScoped<SeedOwner>();
    services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<SeedOwner>());
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var seeder = new RecipeCollectionSeeder(
        scope.ServiceProvider.GetRequiredService<RecipeDbContext>(),
        scope.ServiceProvider.GetRequiredService<ICommandBus>(),
        scope.ServiceProvider.GetRequiredService<SeedOwner>());
    await seeder.ApplyAsync(ready, args[2].Trim(), connectionString, ct);
    return 0;
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    Console.Error.WriteLine("Seed run cancelled.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

internal sealed record SeedCollection(int FormatVersion, SeedEntry[] Recipes)
{
    public SeedEntry[] Validate()
    {
        if (FormatVersion != 1 || Recipes is not { Length: > 0 })
        {
            throw new InvalidDataException("Expected a non-empty recipe collection in seed format version 1.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var descriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ready = new List<SeedEntry>();
        foreach (var entry in Recipes)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.SeedKey) || !keys.Add(entry.SeedKey))
            {
                throw new InvalidDataException("Every entry requires a unique seed key.");
            }

            if (entry.Source is null || entry.Source.RatingStars is < 1 or > 5)
            {
                throw new InvalidDataException($"{entry.SeedKey}: rating must be null or between 1 and 5.");
            }

            if (entry.SeedStatus == "needsReview")
            {
                continue;
            }

            if (entry.SeedStatus != "ready")
            {
                throw new InvalidDataException($"{entry.SeedKey}: unknown seed status '{entry.SeedStatus}'.");
            }

            var description = entry.Recipe?.Description?.Trim();
            if (string.IsNullOrEmpty(description) || !descriptions.Add(description))
            {
                throw new InvalidDataException($"{entry.SeedKey}: a ready recipe requires a unique source description.");
            }

            try
            {
                CreateRecipeCommandValidator.ValidateAndThrow(entry.Recipe!);
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException($"{entry.SeedKey}: {error.Message}", error);
            }

            ready.Add(entry);
        }

        return ready.ToArray();
    }
}

internal sealed record SeedEntry(string SeedKey, string SeedStatus, CreateRecipeCommand Recipe, SeedSource Source);

internal sealed class SeedSource
{
    public required int? RatingStars { get; init; }
}

internal sealed class SeedOwner : ICurrentUser
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsAuthenticated => UserId is not null;
    public bool IsInRole(string roleName) => false;
}
