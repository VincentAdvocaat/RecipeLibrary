namespace RecipeLibrary.Domain.Entities;

/// <summary>Durable start limits; no raw addresses are stored.</summary>
public sealed class DemoStartQuota
{
    public string AddressHash { get; set; } = string.Empty;
    public DateTimeOffset WindowStart { get; set; }
    public int Starts { get; set; }
}
