namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// Groups up to two shopping lists (e.g. split by store).
/// </summary>
public sealed class ShoppingListGroup
{
    public Guid Id { get; set; }

    public string? OwnerUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ShoppingList> Lists { get; set; } = new List<ShoppingList>();
}
