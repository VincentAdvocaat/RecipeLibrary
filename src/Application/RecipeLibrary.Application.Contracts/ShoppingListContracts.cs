namespace RecipeLibrary.Application.Contracts;

public sealed class GetOrCreateShoppingListGroupQuery : IQuery<GetOrCreateShoppingListGroupResult>
{
    public Guid? GroupId { get; init; }

    /// <summary>Entra object ID when shopping lists are user-scoped; otherwise null.</summary>
    public string? OwnerUserId { get; init; }

    /// <summary>Localized format with {0} for the number, e.g. "Boodschappenlijst {0}".</summary>
    public string DefaultListNameFormat { get; init; } = string.Empty;
}

public sealed class GetNextShoppingListNameQuery : IQuery<GetNextShoppingListNameResult>
{
    public string NameFormat { get; init; } = string.Empty;

    /// <summary>When set, only list names in this group are considered; otherwise all lists.</summary>
    public Guid? ScopeGroupId { get; init; }
}

public sealed record GetNextShoppingListNameResult(string Name);

public sealed class GetOrCreateShoppingListGroupResult
{
    public Guid GroupId { get; init; }

    public IReadOnlyList<ShoppingListDto> Lists { get; init; } = [];
}

public sealed class ShoppingListDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int StoreOrder { get; init; }

    public IReadOnlyList<ShoppingListItemDto> Items { get; init; } = [];
}

public sealed class ShoppingListItemDto
{
    public Guid Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Preparation { get; init; }

    public decimal Quantity { get; init; }

    public string Unit { get; init; } = string.Empty;

    public bool IsChecked { get; init; }

    public IReadOnlyList<ShoppingListItemSourceDto> Sources { get; init; } = [];
}

public sealed class ShoppingListItemSourceDto
{
    public Guid RecipeId { get; init; }

    public string RecipeTitle { get; init; } = string.Empty;
}

public sealed class GetShoppingListSummaryQuery : IQuery<ShoppingListSummaryResult>
{
    public Guid GroupId { get; init; }
}

public sealed record ShoppingListSummaryResult(int UncheckedItemCount);

public sealed class AddRecipesToShoppingListCommand : ICommand<AddRecipesToShoppingListResult>
{
    public Guid ShoppingListId { get; init; }

    public IReadOnlyList<Guid> RecipeIds { get; init; } = [];
}

public sealed record AddRecipesToShoppingListResult(int RecipesAdded, int IngredientsAdded);

public sealed class ToggleShoppingListItemCommand : ICommand<ToggleShoppingListItemResult>
{
    public Guid ItemId { get; init; }

    public bool IsChecked { get; init; }
}

public sealed record ToggleShoppingListItemResult(bool IsChecked);

public sealed class RemoveShoppingListItemCommand : ICommand<RemoveShoppingListItemResult>
{
    public Guid ItemId { get; init; }
}

public sealed record RemoveShoppingListItemResult(bool Removed);

public sealed class ClearShoppingListCommand : ICommand<ClearShoppingListResult>
{
    public Guid ShoppingListId { get; init; }
}

public sealed record ClearShoppingListResult(bool Cleared);

public sealed class DeleteShoppingListCommand : ICommand<DeleteShoppingListResult>
{
    public Guid ShoppingListId { get; init; }
}

public sealed record DeleteShoppingListResult(bool Deleted, Guid? RemainingGroupId);

public sealed class DeleteShoppingListGroupCommand : ICommand<DeleteShoppingListGroupResult>
{
    public Guid GroupId { get; init; }
}

public sealed record DeleteShoppingListGroupResult(bool Deleted);

public sealed class SplitShoppingListCommand : ICommand<SplitShoppingListResult>
{
    public Guid GroupId { get; init; }

    public string NewListName { get; init; } = string.Empty;

    public IReadOnlyList<Guid> ItemIds { get; init; } = [];
}

public sealed record SplitShoppingListResult(Guid NewListId, int ItemsMoved);

public sealed class MoveShoppingListItemCommand : ICommand<MoveShoppingListItemResult>
{
    public Guid ItemId { get; init; }

    public Guid TargetShoppingListId { get; init; }
}

public sealed record MoveShoppingListItemResult(bool Moved);

public sealed class UpdateShoppingListNameCommand : ICommand<UpdateShoppingListNameResult>
{
    public Guid ShoppingListId { get; init; }

    public string Name { get; init; } = string.Empty;
}

public sealed record UpdateShoppingListNameResult(bool Updated);
