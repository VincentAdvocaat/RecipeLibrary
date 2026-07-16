using System.Text.Json.Serialization;

namespace RecipeLibrary.Infrastructure.Persistence.Seed;

public sealed class CuratedIngredientCatalogDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("ingredients")]
    public IReadOnlyList<CuratedIngredientEntry> Ingredients { get; init; } = [];
}

public sealed class CuratedIngredientEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("names")]
    public CuratedIngredientNames Names { get; init; } = new();
}

public sealed class CuratedIngredientNames
{
    [JsonPropertyName("nl")]
    public IReadOnlyList<string> Nl { get; init; } = [];

    [JsonPropertyName("en")]
    public IReadOnlyList<string> En { get; init; } = [];
}
