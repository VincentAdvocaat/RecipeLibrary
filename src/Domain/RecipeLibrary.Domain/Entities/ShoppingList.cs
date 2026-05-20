namespace RecipeLibrary.Domain.Entities;

/// <summary>
/// A named shopping list within a group (max two per group).
/// </summary>
public sealed class ShoppingList
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>1 = primary, 2 = secondary (after split).</summary>
    public int StoreOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ShoppingListGroup? Group { get; set; }

    public ICollection<ShoppingListItem> Items { get; set; } = new List<ShoppingListItem>();
}
