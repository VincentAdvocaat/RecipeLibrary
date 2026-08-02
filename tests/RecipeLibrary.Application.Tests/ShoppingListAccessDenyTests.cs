using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.UseCases.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

/// <summary>
/// Fail-closed access: unauthenticated callers and non-owners are denied.
/// </summary>
public sealed class ShoppingListAccessDenyTests
{
    private const string UserB = "user-b";

    [Fact]
    public async Task ClearShoppingList_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task ToggleItem_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task RemoveItem_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new RemoveShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new RemoveShoppingListItemCommand { ItemId = itemId }));

        Assert.Null(repo.LastRemovedItemId);
    }

    [Fact]
    public async Task DeleteGroup_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new DeleteShoppingListGroupCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new DeleteShoppingListGroupCommand { GroupId = groupId }));

        Assert.Null(repo.LastDeletedGroupId);
    }

    [Fact]
    public async Task GetSummary_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            UncheckedItemCount = 99,
        };
        var sut = new GetShoppingListSummaryQueryHandler(repo, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetShoppingListSummaryQuery { GroupId = groupId }));
    }

    [Fact]
    public async Task GetPantryItems_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var pantry = new RecordingPantryRepository();
        var sut = new GetPantryItemsQueryHandler(pantry, shopping, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetPantryItemsQuery { ShoppingListGroupId = groupId }));

        Assert.Null(pantry.LastQueriedOwnerKey);
    }

    [Fact]
    public async Task UpsertPantryItem_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var pantry = new RecordingPantryRepository();
        var sut = new UpsertPantryItemCommandHandler(
            pantry,
            shopping,
            new FixedCurrentUser(UserB),
            new PantryIngredientMerger(new IngredientTextNormalizer()), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpsertPantryItemCommand
            {
                ShoppingListGroupId = groupId,
                DisplayName = "Zout",
            }));

        Assert.Null(pantry.UpsertedItem);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenClearingList()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task WhitespaceOnlyOwner_Throws_WhenClearingList()
    {
        // IsNullOrWhiteSpace (not only IsNullOrEmpty) must deny blank owner ids.
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = true,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new ClearShoppingListCommandHandler(
            repo,
            new FixedCurrentUser("   "),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ClearShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastClearedListId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenGettingSummary()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            UncheckedItemCount = 3,
        };
        var sut = new GetShoppingListSummaryQueryHandler(repo, new AnonymousCurrentUser());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetShoppingListSummaryQuery { GroupId = groupId }));
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenTogglingItem()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task AddManualItem_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] },
        };
        var sut = new AddManualShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser(UserB),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new AddManualShoppingListItemCommand
            {
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = 1,
                Unit = nameof(Unit.Piece),
            }));

        Assert.Null(repo.LastReplacedListId);
    }

    [Fact]
    public async Task UpdateItemQuantity_Throws_WhenListNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new UpdateShoppingListItemQuantityCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpdateShoppingListItemQuantityCommand { ItemId = itemId, Quantity = 3 }));

        Assert.Null(repo.LastQuantityItemId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_BeforeItemLookup_WhenItemMissing()
    {
        var itemId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = itemId, IsChecked = true }));

        Assert.Null(repo.LastToggledItemId);
    }

    [Fact]
    public async Task ToggleItem_Throws_WhenItemMissing_ForAuthenticatedUser()
    {
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new ToggleShoppingListItemCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleAsync(new ToggleShoppingListItemCommand { ItemId = Guid.NewGuid(), IsChecked = true }));
    }

    [Fact]
    public async Task UpdateListName_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new UpdateShoppingListNameCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = listId, Name = "Other" }));

        Assert.Null(repo.LastUpdatedNameListId);
    }

    [Fact]
    public async Task DeleteList_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid() },
        };
        var sut = new DeleteShoppingListCommandHandler(repo, new FixedCurrentUser(UserB), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new DeleteShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(repo.LastDeletedListId);
    }

    [Fact]
    public async Task SplitList_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new SplitShoppingListCommandHandler(
            repo,
            new FixedCurrentUser(UserB),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new SplitShoppingListCommand
            {
                GroupId = groupId,
                NewListName = "Second",
                ItemIds = [Guid.NewGuid()],
            }));

        Assert.Null(repo.LastAddedListName);
    }

    [Fact]
    public async Task MoveItem_Throws_WhenItemNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Melk",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var sut = new MoveShoppingListItemCommandHandler(
            repo,
            new FixedCurrentUser(UserB),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new MoveShoppingListItemCommand
            {
                ItemId = itemId,
                TargetShoppingListId = Guid.NewGuid(),
            }));

        Assert.Empty(repo.ReplacedItemsByListId);
    }

    [Fact]
    public async Task MoveItemToPantry_Throws_WhenItemNotAccessible()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var pantry = new RecordingPantryRepository();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shopping,
            pantry,
            new FixedCurrentUser(UserB),
            new NoOpUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId }));

        Assert.Null(shopping.LastRemovedItemId);
        Assert.Null(pantry.UpsertedItem);
    }

    [Fact]
    public async Task AddRecipes_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] },
        };
        var sut = new AddRecipesToShoppingListCommandHandler(
            new EmptyRecipeRepository(),
            shopping,
            new RecordingPantryRepository(),
            new FixedCurrentUser(UserB),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new PantryExclusionFilter(new PantryIngredientMerger(new IngredientTextNormalizer())),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new AddRecipesToShoppingListCommand
            {
                ShoppingListId = listId,
                RecipeIds = [Guid.NewGuid()],
            }));

        Assert.Null(shopping.LastReplacedListId);
    }

    [Fact]
    public async Task ApplyPantry_Throws_WhenListNotAccessible()
    {
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] },
        };
        var pantry = new RecordingPantryRepository();
        var sut = new ApplyPantryToShoppingListCommandHandler(
            shopping,
            pantry,
            new FixedCurrentUser(UserB),
            new PantryExclusionFilter(new PantryIngredientMerger(new IngredientTextNormalizer())),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new ApplyPantryToShoppingListCommand { ShoppingListId = listId }));

        Assert.Null(shopping.LastReplacedListId);
        Assert.Null(pantry.LastQueriedOwnerKey);
    }

    [Fact]
    public async Task RemovePantryItem_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var pantry = new RecordingPantryRepository();
        var sut = new RemovePantryItemCommandHandler(
            pantry,
            shopping,
            new FixedCurrentUser(UserB),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new RemovePantryItemCommand
            {
                ShoppingListGroupId = groupId,
                ItemId = Guid.NewGuid(),
            }));

        Assert.Null(pantry.LastRemovedItemId);
    }

    [Fact]
    public async Task GetNextListName_Throws_WhenGroupNotAccessible()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            ListNames = ["Secret 1"],
        };
        var sut = new GetNextShoppingListNameQueryHandler(repo, new FixedCurrentUser(UserB));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetNextShoppingListNameQuery
            {
                NameFormat = "Shop {0}",
                ScopeGroupId = groupId,
            }));
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenDeletingGroup()
    {
        var groupId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new DeleteShoppingListGroupCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new DeleteShoppingListGroupCommand { GroupId = groupId }));

        Assert.Null(repo.LastDeletedGroupId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenUpdatingListName()
    {
        var listId = Guid.NewGuid();
        var repo = new RecordingShoppingListRepository { AccessibleByDefault = false };
        var sut = new UpdateShoppingListNameCommandHandler(repo, new AnonymousCurrentUser(), new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new UpdateShoppingListNameCommand { ShoppingListId = listId, Name = "Other" }));

        Assert.Null(repo.LastUpdatedNameListId);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenGettingNextListName()
    {
        var sut = new GetNextShoppingListNameQueryHandler(
            new RecordingShoppingListRepository { ListNames = ["Shop 1"] },
            new AnonymousCurrentUser());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new GetNextShoppingListNameQuery { NameFormat = "Shop {0}" }));
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenMovingItemToPantry()
    {
        var itemId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            Item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                DisplayName = "Zout",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };
        var pantry = new RecordingPantryRepository();
        var sut = new MoveShoppingListItemToPantryCommandHandler(
            shopping,
            pantry,
            new AnonymousCurrentUser(),
            new NoOpUnitOfWork(),
            new PantryIngredientMerger(new IngredientTextNormalizer()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new MoveShoppingListItemToPantryCommand { ItemId = itemId }));

        Assert.Null(shopping.LastRemovedItemId);
        Assert.Null(pantry.UpsertedItem);
    }

    [Fact]
    public async Task AnonymousUser_Throws_WhenAddingRecipes()
    {
        var listId = Guid.NewGuid();
        var shopping = new RecordingShoppingListRepository
        {
            AccessibleByDefault = false,
            List = new ShoppingList { Id = listId, GroupId = Guid.NewGuid(), Items = [] },
        };
        var sut = new AddRecipesToShoppingListCommandHandler(
            new EmptyRecipeRepository(),
            shopping,
            new RecordingPantryRepository(),
            new AnonymousCurrentUser(),
            new ShoppingListIngredientMerger(new IngredientTextNormalizer()),
            new PantryExclusionFilter(new PantryIngredientMerger(new IngredientTextNormalizer())),
            new NoOpUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.HandleAsync(new AddRecipesToShoppingListCommand
            {
                ShoppingListId = listId,
                RecipeIds = [Guid.NewGuid()],
            }));

        Assert.Null(shopping.LastReplacedListId);
    }

    private sealed class EmptyRecipeRepository : IRecipeRepository
    {
        public Task AddAsync(Recipe recipe, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task DeleteAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Recipe?> GetByIdAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<Recipe?> GetByIdForUpdateAsync(string ownerUserId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<IReadOnlyList<Recipe>> GetByIdsAsync(
            string ownerUserId,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task<IReadOnlyList<string>> GetIngredientTagNamesForRecipeAsync(
            string ownerUserId,
            Guid recipeId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Recipe>> GetListAsync(
            string ownerUserId,
            string? search,
            RecipeCategory? category,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Recipe>>([]);

        public Task<bool> IsRecipeImageAccessibleAsync(
            string ownerUserId,
            string fileName,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task UpdateAsync(string ownerUserId, Recipe recipe, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
