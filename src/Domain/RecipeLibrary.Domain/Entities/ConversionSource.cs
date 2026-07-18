namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Trusted conversion source (King Arthur, USDA, Manual). AI is not a source.
/// </summary>
public sealed class ConversionSource
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
