namespace RecipeLibrary.Application.Contracts;

public sealed class GetPantryItemsQuery : IQuery<GetPantryItemsResult>
{
    public Guid ShoppingListGroupId { get; init; }
}

public sealed class GetPantryItemsResult
{
    public IReadOnlyList<PantryItemDto> Items { get; init; } = [];
}

public sealed class PantryItemDto
{
    public Guid Id { get; init; }

    public Guid? CanonicalIngredientId { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class UpsertPantryItemCommand : ICommand<UpsertPantryItemResult>
{
    public Guid ShoppingListGroupId { get; init; }

    public Guid? CanonicalIngredientId { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

public sealed record UpsertPantryItemResult(bool Upserted, Guid ItemId);

public sealed class RemovePantryItemCommand : ICommand<RemovePantryItemResult>
{
    public Guid ShoppingListGroupId { get; init; }

    public Guid ItemId { get; init; }
}

public sealed record RemovePantryItemResult(bool Removed);

public sealed class ApplyPantryToShoppingListCommand : ICommand<ApplyPantryToShoppingListResult>
{
    public Guid ShoppingListId { get; init; }
}

public sealed record ApplyPantryToShoppingListResult(int ItemsRemoved);

public sealed class MoveShoppingListItemToPantryCommand : ICommand<MoveShoppingListItemToPantryResult>
{
    public Guid ItemId { get; init; }
}

public sealed record MoveShoppingListItemToPantryResult(bool Moved, Guid? PantryItemId);
